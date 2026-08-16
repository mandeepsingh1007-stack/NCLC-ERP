using System.Security.Claims;
using System.Collections.Generic;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Platform.Core.Auth;
using Platform.Core.Cache;
using Platform.Core.Metadata;
using Platform.Core.Runtime;
using Platform.Data.Repositories;

namespace Platform.API.Endpoints;

/// <summary>
/// Generic Data API — CRUD for any registered table.
/// Enforces: table allowlist, column allowlist, parameterized SQL, pagination, filtering.
/// RBAC: IPermissionService checks on every operation.
/// Tenant isolation: QueryBuilder injects @ClientId/@OrgId predicates from IReadOnlyContext.
/// </summary>
public static class DataEndpoints
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 500;

    /// <summary>
    /// Extract userId from ClaimsPrincipal (JWT). Returns null if not authenticated.
    /// </summary>
    private static int? GetUserId(ClaimsPrincipal user)
    {
        var userIdStr = user.FindFirst(AuthClaimTypes.UserId)?.Value;
        return int.TryParse(userIdStr, out var uid) ? uid : (int?)null;
    }

    /// <summary>
    /// Return a forbidden response based on permission result, or null if allowed.
    /// </summary>
    private static Microsoft.AspNetCore.Http.IResult? CheckPermission(PermissionResult perm)
    {
        if (perm.Allowed) return null;
        return Results.Problem(
            detail: perm.Reason ?? "Access denied.",
            statusCode: 403,
            extensions: new Dictionary<string, object?> { { "error", new { code = "AccessDenied", message = perm.Reason } } });
    }

    public static IEndpointRouteBuilder MapGenericDataEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/data");

        // GET /api/data/{table} — list with pagination/filtering/sorting
        group.MapGet("/{table}", async (
            string table,
            [FromQuery] int page,
            [FromQuery] int pageSize,
            [FromQuery] string? sortBy,
            [FromQuery] string? sortDir,
            [FromQuery] string? filter,
            [FromQuery] string? columns,
            ClaimsPrincipal user,
            IReadOnlyContext context,
            IPermissionService permissionService,
            QueryBuilder queryBuilder,
            IMetadataGraph metadataGraph,
            NpgsqlConnection connection) =>
        {
            // --- Authorization ---
            var userId = GetUserId(user);
            if (userId == null)
                return Results.Unauthorized();

            var readPerm = await permissionService.CanReadTableAsync(userId.Value, table, PermissionLevel.ReadOnly);
            var authResp = CheckPermission(readPerm);
            if (authResp != null)
                return authResp;

            // --- Validate table ---
            var quotedTable = queryBuilder.ValidateTable(table);
            if (quotedTable == null)
                return Results.BadRequest(new { error = new { code = "TableNotAllowed", message = $"Table '{table}' is not allowed." } });

            // --- Validate pageSize ---
            if (pageSize < 1 || pageSize > MaxPageSize)
                return Results.BadRequest(new { error = new { code = "InvalidPageSize", message = $"pageSize must be between 1 and {MaxPageSize}." } });

            // --- Parse filter AST ---
            NpgsqlParameter[]? filterParams = null;
            string? filterSql = null;
            if (!string.IsNullOrWhiteSpace(filter))
            {
                try
                {
                    var tableColumns = metadataGraph.GetColumns(table);
                    var columnNames = tableColumns.Select(c => c.ColumnName).ToList();
                    var parser = new FilterParser();
                    var validated = parser.Parse(filter, columnNames);

                    filterSql = validated.SqlWhereClause;
                    filterParams = validated.Parameters;
                }
                catch (JsonException)
                {
                    return Results.BadRequest(new { error = new { code = "InvalidFilter", message = "Filter contains invalid JSON." } });
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { error = new { code = "InvalidFilter", message = ex.Message } });
                }
            }

            // --- Parse columns (filter by allowed columns) ---
            IEnumerable<string>? columnList = null;
            if (!string.IsNullOrWhiteSpace(columns))
            {
                columnList = columns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }
            else
            {
                // Default: only return columns the user has permission to read
                var allowedCols = await permissionService.GetAllowedColumnsAsync(userId.Value, table, PermissionLevel.ReadOnly);
                if (allowedCols.Count > 0)
                {
                    columnList = allowedCols;
                }
            }

            // --- Record-level filter (private access / record filters) ---
            var recordFilter = await permissionService.GetRecordFilterAsync(userId.Value, table);
            if (!string.IsNullOrEmpty(recordFilter))
            {
                if (string.IsNullOrEmpty(filterSql))
                    filterSql = recordFilter;
                else
                    filterSql = $"({filterSql}) AND ({recordFilter})";
            }

            // --- Build the query ---
            try
            {
                var effectivePage = Math.Max(page, 1);
                var (sql, paramsObj, countSql) = queryBuilder.BuildSelect(
                    table, context, effectivePage, pageSize, sortBy, sortDir, filterSql, filterParams, columnList);

                var allParams = (object[])paramsObj;

                // Execute data query
                var items = await connection.QueryAsync<dynamic>(sql, allParams);

                // Execute count query
                var count = await connection.QuerySingleAsync<int>(countSql, allParams);

                var itemsList = items.ToList();
                var result = new
                {
                    items = itemsList,
                    pagination = new
                    {
                        page = effectivePage,
                        pageSize,
                        totalItems = count,
                        totalPages = (int)Math.Ceiling(count / (double)pageSize)
                    }
                };

                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = new { code = "InvalidParameter", message = ex.Message } });
            }
        });

        // GET /api/data/{table}/{id} — single record
        group.MapGet("/{table}/{id:int}", async (
            string table,
            int id,
            ClaimsPrincipal user,
            IPermissionService permissionService,
            NpgsqlConnection connection) =>
        {
            // --- Authorization ---
            var userId = GetUserId(user);
            if (userId == null)
                return Results.Unauthorized();

            var readPerm = await permissionService.CanReadTableAsync(userId.Value, table, PermissionLevel.ReadOnly);
            var authResp = CheckPermission(readPerm);
            if (authResp != null)
                return authResp;

            // --- Validate table exists ---
            var metaTable = await connection.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT ""TableName"", ""ClassName"" FROM ""SysTable"" WHERE ""IsActive"" = true AND ""TableName"" = @Table",
                new { Table = table });

            if (metaTable == null)
                return Results.BadRequest(new { error = new { code = "TableNotAllowed", message = $"Table '{table}' is not allowed." } });

            // --- Find PK column ---
            var pkCol = await connection.QueryFirstOrDefaultAsync<string>(
                @"SELECT ""ColumnName"" FROM ""SysColumn"" c
                  JOIN ""SysTable"" t ON c.""SysTable_ID"" = t.""SysTable_ID""
                  WHERE t.""TableName"" = @Table AND c.""IsKey"" = true AND c.""IsActive"" = true",
                new { Table = table });

            var pk = pkCol ?? "SysTable_ID";
            var sql = $"SELECT * FROM \"{table}\" WHERE \"{pk}\" = @Id";
            var row = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new { Id = id });

            if (row == null)
                return Results.NotFound(new { error = new { code = "RecordNotFound", message = $"Record {id} not found in table {table}." } });

            // --- Private record check ---
            var privateIds = await permissionService.GetPrivateRecordIdsAsync(userId.Value, table);
            if (privateIds.Count > 0 && !privateIds.Contains(id))
                return Results.Problem(
                    detail: "Record not accessible.",
                    statusCode: 403,
                    extensions: new Dictionary<string, object?> { { "error", new { code = "AccessDenied", message = "Record not accessible." } } });

            return Results.Ok(row);
        });

        // POST /api/data/{table} — create record
        group.MapPost("/{table}", async (
            string table,
            [FromBody] Dictionary<string, object?> data,
            ClaimsPrincipal user,
            IReadOnlyContext context,
            IPermissionService permissionService,
            QueryBuilder queryBuilder,
            IMetadataGraph metadataGraph,
            NpgsqlConnection connection) =>
        {
            // --- Authorization ---
            var userId = GetUserId(user);
            if (userId == null)
                return Results.Unauthorized();

            var writePerm = await permissionService.CanWriteTableAsync(userId.Value, table, PermissionLevel.Create);
            var authResp = CheckPermission(writePerm);
            if (authResp != null)
                return authResp;

            // --- Validate table ---
            var quotedTable = queryBuilder.ValidateTable(table);
            if (quotedTable == null)
                return Results.BadRequest(new { error = new { code = "TableNotAllowed", message = $"Table '{table}' is not allowed." } });

            // --- Build and execute INSERT ---
            try
            {
                var (sql, parameters) = queryBuilder.BuildInsert(table, data, context);

                // Add a placeholder for the RETURNING clause
                sql += " RETURNING *";

                var rows = await connection.QueryAsync<dynamic>(sql, (object[])parameters);
                var createdRow = rows.FirstOrDefault();

                if (createdRow == null)
                    return Results.Problem(
                    detail: "Insert succeeded but no row was returned.",
                    statusCode: 500,
                    extensions: new Dictionary<string, object?> { { "error", new { code = "CreateFailed" } } });

                return Results.Created($"/api/data/{table}/{createdRow}", createdRow);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = new { code = "ValidationError", message = ex.Message } });
            }
        });

        // PUT /api/data/{table}/{id} — update record
        group.MapPut("/{table}/{id:int}", async (
            string table,
            int id,
            [FromBody] Dictionary<string, object?> data,
            ClaimsPrincipal user,
            IReadOnlyContext context,
            IPermissionService permissionService,
            QueryBuilder queryBuilder,
            IMetadataGraph metadataGraph,
            NpgsqlConnection connection) =>
        {
            // --- Authorization ---
            var userId = GetUserId(user);
            if (userId == null)
                return Results.Unauthorized();

            var writePerm = await permissionService.CanWriteTableAsync(userId.Value, table, PermissionLevel.ReadWrite);
            var authResp = CheckPermission(writePerm);
            if (authResp != null)
                return authResp;

            // --- Validate table ---
            var quotedTable = queryBuilder.ValidateTable(table);
            if (quotedTable == null)
                return Results.BadRequest(new { error = new { code = "TableNotAllowed", message = $"Table '{table}' is not allowed." } });

            // --- Find PK column ---
            var pkCol = await connection.QueryFirstOrDefaultAsync<string>(
                @"SELECT ""ColumnName"" FROM ""SysColumn"" c
                  JOIN ""SysTable"" t ON c.""SysTable_ID"" = t.""SysTable_ID""
                  WHERE t.""TableName"" = @Table AND c.""IsKey"" = true AND c.""IsActive"" = true",
                new { Table = table });

            var pk = pkCol ?? "SysTable_ID";

            // --- Private record check ---
            var privateIds = await permissionService.GetPrivateRecordIdsAsync(userId.Value, table);
            if (privateIds.Count > 0 && !privateIds.Contains(id))
                return Results.Problem(
                    detail: "Record not accessible.",
                    statusCode: 403,
                    extensions: new Dictionary<string, object?> { { "error", new { code = "AccessDenied", message = "Record not accessible." } } });

            // --- Build and execute UPDATE ---
            try
            {
                var (sql, parameters) = queryBuilder.BuildUpdate(table, pk, data, context);

                // Set the actual ID value
                var allParams = parameters.ToList();
                var idParam = allParams.First(p => p.ParameterName == "@Id");
                idParam.Value = id;

                var affected = await connection.ExecuteAsync(sql, (IList<NpgsqlParameter>)allParams);
                if (affected == 0)
                    return Results.NotFound(new { error = new { code = "RecordNotFound", message = $"Record {id} not found in table {table}." } });

                // Return the updated record
                var selectSql = $"SELECT * FROM \"{table}\" WHERE \"{pk}\" = @Id";
                var updatedRow = await connection.QueryFirstOrDefaultAsync<dynamic>(selectSql, new { Id = id });

                return Results.Ok(updatedRow);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = new { code = "ValidationError", message = ex.Message } });
            }
        });

        // DELETE /api/data/{table}/{id} — delete record
        group.MapDelete("/{table}/{id:int}", async (
            string table,
            int id,
            ClaimsPrincipal user,
            IReadOnlyContext context,
            IPermissionService permissionService,
            QueryBuilder queryBuilder,
            IMetadataGraph metadataGraph,
            NpgsqlConnection connection) =>
        {
            // --- Authorization ---
            var userId = GetUserId(user);
            if (userId == null)
                return Results.Unauthorized();

            var writePerm = await permissionService.CanWriteTableAsync(userId.Value, table, PermissionLevel.FullControl);
            var authResp = CheckPermission(writePerm);
            if (authResp != null)
                return authResp;

            // --- Validate table ---
            var quotedTable = queryBuilder.ValidateTable(table);
            if (quotedTable == null)
                return Results.BadRequest(new { error = new { code = "TableNotAllowed", message = $"Table '{table}' is not allowed." } });

            // --- Find PK column ---
            var pkCol = await connection.QueryFirstOrDefaultAsync<string>(
                @"SELECT ""ColumnName"" FROM ""SysColumn"" c
                  JOIN ""SysTable"" t ON c.""SysTable_ID"" = t.""SysTable_ID""
                  WHERE t.""TableName"" = @Table AND c.""IsKey"" = true AND c.""IsActive"" = true",
                new { Table = table });

            var pk = pkCol ?? "SysTable_ID";

            // --- Private record check ---
            var privateIds = await permissionService.GetPrivateRecordIdsAsync(userId.Value, table);
            if (privateIds.Count > 0 && !privateIds.Contains(id))
                return Results.Problem(
                    detail: "Record not accessible.",
                    statusCode: 403,
                    extensions: new Dictionary<string, object?> { { "error", new { code = "AccessDenied", message = "Record not accessible." } } });

            // --- Build and execute DELETE ---
            try
            {
                var (sql, parameters) = queryBuilder.BuildDelete(table, pk, context);

                // Set the actual ID value on the @Id placeholder
                var idParam = parameters.FirstOrDefault(p => p.ParameterName == "@Id");
                if (idParam != null)
                {
                    idParam.Value = id;
                }

                var affected = await connection.ExecuteAsync(sql, (IList<NpgsqlParameter>)parameters);
                if (affected == 0)
                    return Results.NotFound(new { error = new { code = "RecordNotFound", message = $"Record {id} not found in table {table}." } });

                return Results.NoContent();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = new { code = "ValidationError", message = ex.Message } });
            }
        });

        return app;
    }
}
