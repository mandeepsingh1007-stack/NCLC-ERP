using System.Collections.Generic;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Platform.Core.Cache;
using Platform.Core.Metadata;
using Platform.Core.Runtime;
using Platform.Data.Repositories;

namespace Platform.API.Endpoints;

/// <summary>
/// Generic Data API — CRUD for any registered table.
/// Enforces: table allowlist, column allowlist, parameterized SQL, pagination, filtering.
/// Phase 3: null context (no tenant/org filtering). Phase 4: wired from JWT.
/// </summary>
public static class DataEndpoints
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 500;

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
            IReadOnlyContext context,
            QueryBuilder queryBuilder,
            IMetadataGraph metadataGraph,
            NpgsqlConnection connection) =>
        {
            int effectivePage = Math.Max(page, 1);

            // Validate table
            var quotedTable = queryBuilder.ValidateTable(table);
            if (quotedTable == null)
                return Results.BadRequest(new { error = new { code = "TableNotAllowed", message = $"Table '{table}' is not allowed." } });

            // Validate pageSize
            if (pageSize < 1 || pageSize > MaxPageSize)
                return Results.BadRequest(new { error = new { code = "InvalidPageSize", message = $"pageSize must be between 1 and {MaxPageSize}." } });

            // Parse filter AST
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

            // Parse columns
            IEnumerable<string>? columnList = null;
            if (!string.IsNullOrWhiteSpace(columns))
            {
                columnList = columns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }

            // Build the query
            try
            {
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
            NpgsqlConnection connection) =>
        {
            var metaTable = await connection.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT ""TableName"", ""ClassName"" FROM ""SysTable"" WHERE ""IsActive"" = true AND ""TableName"" = @Table",
                new { Table = table });

            if (metaTable == null)
                return Results.BadRequest(new { error = new { code = "TableNotAllowed", message = $"Table '{table}' is not allowed." } });

            // Find PK column
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

            return Results.Ok(row);
        });

        // POST /api/data/{table} — create (placeholder for Phase 4)
        group.MapPost("/{table}", async () =>
        {
            return Results.Problem(
                detail: "Create requires authenticated context (Phase 4).",
                statusCode: 501,
                extensions: new Dictionary<string, object?> { { "error", new { code = "NotImplemented" } } });
        });

        // PUT /api/data/{table}/{id} — update (placeholder for Phase 4)
        group.MapPut("/{table}/{id:int}", async () =>
        {
            return Results.Problem(
                detail: "Update requires authenticated context (Phase 4).",
                statusCode: 501,
                extensions: new Dictionary<string, object?> { { "error", new { code = "NotImplemented" } } });
        });

        // DELETE /api/data/{table}/{id} — delete (placeholder for Phase 4)
        group.MapDelete("/{table}/{id:int}", async () =>
        {
            return Results.Problem(
                detail: "Delete requires authenticated context (Phase 4).",
                statusCode: 501,
                extensions: new Dictionary<string, object?> { { "error", new { code = "NotImplemented" } } });
        });

        return app;
    }
}
