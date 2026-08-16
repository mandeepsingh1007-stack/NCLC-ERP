using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Npgsql;
using Platform.Core.Auth;
using Platform.Core.Metadata;
using Platform.Core.Runtime;
using Platform.Data.Repositories;

namespace Platform.API.Endpoints;

/// <summary>
/// Generic Lookup API — resolves reference data for lookups (LIST, TABLE, SEARCH).
/// Enforces:
///   - Authentication (JWT)
///   - Authorization (table-level CanReadTableAsync)
///   - Tenant isolation (SysClient_ID predicate from IReadOnlyContext)
///   - Organization isolation (SysOrg_ID predicate from IReadOnlyContext)
///   - Column permissions (key/display columns must be readable)
/// Caches lookups in IMemoryCache + Redis.
/// </summary>
[Authorize]
public static class LookupEndpoints
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private const int MaxLookupPageSize = 500;

    public static IEndpointRouteBuilder MapGenericLookupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lookup");

        // GET /api/lookup/{referenceId}
        group.MapGet("/{referenceId:int}", async (
            int referenceId,
            [FromQuery] int page,
            [FromQuery] int pageSize,
            [FromQuery] string? search,
            ClaimsPrincipal user,
            IReadOnlyContext context,
            IPermissionService permissionService,
            SysReferenceRepository referenceRepo,
            SysReferenceListRepository referenceListRepo,
            SysReferenceTableRepository referenceTableRepo,
            SysTableRepository sysTableRepo,
            SysColumnRepository sysColumnRepo,
            Npgsql.NpgsqlConnection connection,
            IMetadataGraph metadataGraph,
            IDistributedCache cache) =>
        {
            // --- Authentication ---
            var userId = GetUserUserId(user);
            if (userId == null)
                return Results.Unauthorized();

            // --- Validate reference exists ---
            var reference = referenceRepo.GetById(referenceId);
            if (reference == null)
                return Results.NotFound(new { error = new { code = "ReferenceNotFound", message = $"Reference with ID {referenceId} not found." } });

            pageSize = Math.Min(pageSize, MaxLookupPageSize);
            if (pageSize < 1) pageSize = 50;
            var effectivePage = Math.Max(page, 1);

            var validationType = reference.ValidationType;

            switch (validationType)
            {
                case ValidationTypeEnum.List:
                    {
                        // LIST references are metadata-level fixed sets (Yes/No, status enums, etc.).
                        // They do NOT contain tenant-scoped business data.
                        // AuthN is enforced by [Authorize]. AuthZ not required for static metadata.
                        var result = ResolveListReference(userId.Value, referenceId, effectivePage, pageSize, reference, referenceListRepo);
                        var json = System.Text.Json.JsonSerializer.Serialize(result);
                        await cache.SetStringAsync(cacheKeyLookup(referenceId, effectivePage, pageSize, search), json, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl });
                        return Results.Ok(System.Text.Json.JsonSerializer.Deserialize<dynamic>(json));
                    }

                case ValidationTypeEnum.Table:
                    {
                        // TABLE references read from business tables — FULL security required.
                        var tableName = ResolveTargetTableName(referenceTableRepo, sysTableRepo, referenceId);
                        if (tableName == null)
                            return Results.BadRequest(new { error = new { code = "NoReferenceTable", message = $"No target table configured for reference {referenceId}." } });

                        // Authorization: table-level read permission
                        var readPerm = await permissionService.CanReadTableAsync(userId.Value, tableName, PermissionLevel.ReadOnly);
                        if (!readPerm.Allowed)
                            return Results.Problem(
                                detail: readPerm.Reason ?? "Access denied.",
                                statusCode: 403,
                                extensions: new Dictionary<string, object?> { { "error", new { code = "AccessDenied", message = readPerm.Reason } } });

                        // Column permission: verify key and display columns are readable
                        var tableRef = referenceTableRepo.GetByReferenceId(referenceId).FirstOrDefault();
                        if (tableRef == null)
                            return Results.BadRequest(new { error = new { code = "NoReferenceTable", message = $"No target table configured for reference {referenceId}." } });

                        var columns = sysColumnRepo.GetByTableId(tableRef.SysTableId);
                        var keyCol = columns.FirstOrDefault(c => c.ColumnName == tableRef.KeyColumn);
                        var displayCol = columns.FirstOrDefault(c => c.ColumnName == tableRef.DisplayColumn);

                        if (keyCol == null || displayCol == null)
                            return Results.BadRequest(new { error = new { code = "InvalidFieldMapping", message = $"KeyColumn '{tableRef.KeyColumn}' or DisplayColumn '{tableRef.DisplayColumn}' not found." } });

                        var keyPerm = await permissionService.CheckColumnAsync(userId.Value, tableName, tableRef.KeyColumn, PermissionLevel.ReadOnly);
                        var displayPerm = await permissionService.CheckColumnAsync(userId.Value, tableName, tableRef.DisplayColumn, PermissionLevel.ReadOnly);

                        if (!keyPerm.Allowed || !displayPerm.Allowed)
                            return Results.Problem(
                                detail: "Insufficient column-level permission.",
                                statusCode: 403,
                                extensions: new Dictionary<string, object?> { { "error", new { code = "AccessDenied", message = "Insufficient column-level permission." } } });

                        var result = await ResolveTableReferenceWithSecurity(effectivePage, pageSize, search, reference, tableRef, tableName, keyCol.ColumnName, displayCol.ColumnName, context, connection);
                        if (result is IResult result1)
                            return result1;

                        var json = System.Text.Json.JsonSerializer.Serialize(result);
                        await cache.SetStringAsync(cacheKeyLookup(referenceId, effectivePage, pageSize, search), json, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl });
                        return Results.Ok(System.Text.Json.JsonSerializer.Deserialize<dynamic>(json));
                    }

                case ValidationTypeEnum.Search:
                    {
                        // SEARCH references also read from business tables — FULL security required.
                        var tableName = ResolveTargetTableName(referenceTableRepo, sysTableRepo, referenceId);
                        if (tableName == null)
                            return Results.BadRequest(new { error = new { code = "NoReferenceTable", message = $"No target table configured for reference {referenceId}." } });

                        // Authorization: table-level read permission
                        var readPerm = await permissionService.CanReadTableAsync(userId.Value, tableName, PermissionLevel.ReadOnly);
                        if (!readPerm.Allowed)
                            return Results.Problem(
                                detail: readPerm.Reason ?? "Access denied.",
                                statusCode: 403,
                                extensions: new Dictionary<string, object?> { { "error", new { code = "AccessDenied", message = readPerm.Reason } } });

                        var tableRef = referenceTableRepo.GetByReferenceId(referenceId).FirstOrDefault();
                        if (tableRef == null)
                            return Results.BadRequest(new { error = new { code = "NoReferenceTable", message = $"No target table configured for reference {referenceId}." } });

                        var columns = sysColumnRepo.GetByTableId(tableRef.SysTableId);
                        var keyCol = columns.FirstOrDefault(c => c.ColumnName == tableRef.KeyColumn);
                        var displayCol = columns.FirstOrDefault(c => c.ColumnName == tableRef.DisplayColumn);

                        if (keyCol == null || displayCol == null)
                            return Results.BadRequest(new { error = new { code = "InvalidFieldMapping", message = $"KeyColumn '{tableRef.KeyColumn}' or DisplayColumn '{tableRef.DisplayColumn}' not found." } });

                        var keyPerm = await permissionService.CheckColumnAsync(userId.Value, tableName, tableRef.KeyColumn, PermissionLevel.ReadOnly);
                        var displayPerm = await permissionService.CheckColumnAsync(userId.Value, tableName, tableRef.DisplayColumn, PermissionLevel.ReadOnly);

                        if (!keyPerm.Allowed || !displayPerm.Allowed)
                            return Results.Problem(
                                detail: "Insufficient column-level permission.",
                                statusCode: 403,
                                extensions: new Dictionary<string, object?> { { "error", new { code = "AccessDenied", message = "Insufficient column-level permission." } } });

                        var result = await ResolveSearchReferenceWithSecurity(effectivePage, pageSize, search, reference, tableRef, tableName, keyCol.ColumnName, displayCol.ColumnName, context, connection);
                        if (result is IResult result2)
                            return result2;

                        var json = System.Text.Json.JsonSerializer.Serialize(result);
                        await cache.SetStringAsync(cacheKeyLookup(referenceId, effectivePage, pageSize, search), json, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl });
                        return Results.Ok(System.Text.Json.JsonSerializer.Deserialize<dynamic>(json));
                    }

                default:
                    return Results.BadRequest(new { error = new { code = "InvalidReferenceType", message = $"Unknown validation type: {reference.ValidationType}" } });
            }
        });

        return app;
    }

    // ---- Helper: cache key (avoids search=null interpolation issues) ----
    private static string cacheKeyLookup(int referenceId, int page, int pageSize, string? search)
        => $"lookup:{referenceId}:{page}:{pageSize}:{search ?? ""}";

    // ---- Helper: extract userId from ClaimsPrincipal ----
    private static int? GetUserUserId(ClaimsPrincipal user)
    {
        var userIdStr = user.FindFirst(AuthClaimTypes.UserId)?.Value;
        return int.TryParse(userIdStr, out var uid) ? uid : (int?)null;
    }

    // ---- Helper: resolve target table name from reference ----
    private static string? ResolveTargetTableName(SysReferenceTableRepository refTableRepo, SysTableRepository sysTableRepo, int referenceId)
    {
        var tableRefs = refTableRepo.GetByReferenceId(referenceId).ToList();
        if (!tableRefs.Any()) return null;
        var tableRef = tableRefs.First();
        var sysTable = sysTableRepo.GetById(tableRef.SysTableId);
        return sysTable?.TableName;
    }

    // ---- LIST reference (metadata-level, no tenant scoping needed) ----
    private static object ResolveListReference(
        int userId,
        int referenceId,
        int page,
        int pageSize,
        SysReference reference,
        SysReferenceListRepository referenceListRepo)
    {
        var items = referenceListRepo.GetByReferenceId(referenceId).ToList();
        var totalCount = items.Count;
        var paged = items.Skip((page - 1) * pageSize).Take(pageSize);

        return new
        {
            referenceName = reference.Name,
            totalItems = totalCount,
            pagination = new { page, pageSize, totalItems = totalCount, totalPages = (int)Math.Ceiling(totalCount / (double)pageSize) },
            items = paged.Select(i => new
            {
                value = i.Value,
                display = i.Name
            })
        };
    }

    // ---- TABLE reference with tenant/org isolation ----
    private static async Task<object> ResolveTableReferenceWithSecurity(
        int page,
        int pageSize,
        string? search,
        SysReference reference,
        SysReferenceTable tableRef,
        string tableName,
        string keyColumnName,
        string displayColumnName,
        IReadOnlyContext context,
        Npgsql.NpgsqlConnection connection)
    {
        var quotedTable = $"\"{tableName}\"";
        var quotedKey = $"\"{keyColumnName}\"";
        var quotedDisplay = $"\"{displayColumnName}\"";

        var parameters = new List<NpgsqlParameter>();
        var predicates = new List<string>();

        // Search filter (parameterized)
        string? whereClause = null;
        if (!string.IsNullOrWhiteSpace(search))
        {
            whereClause = $"{quotedDisplay} ILIKE @Search";
            parameters.Add(new NpgsqlParameter("@Search", $"%{search}%"));
        }

        // Tenant isolation predicate
        if (!string.IsNullOrWhiteSpace(context.TenantPredicate))
        {
            predicates.Add(context.TenantPredicate!);
            if (!string.IsNullOrWhiteSpace(context.TenantId))
                parameters.Add(new NpgsqlParameter("@ClientId", context.TenantId!));
        }

        // Organization isolation predicate
        if (!string.IsNullOrWhiteSpace(context.OrgPredicate))
        {
            predicates.Add(context.OrgPredicate!);
            if (!string.IsNullOrWhiteSpace(context.OrgId))
                parameters.Add(new NpgsqlParameter("@OrgId", context.OrgId!));
        }

        // Combine WHERE clauses
        var searchWhere = !string.IsNullOrWhiteSpace(whereClause) ? $" WHERE {whereClause}" : "";
        var tenantWhere = predicates.Count > 0 ? " WHERE " + string.Join(" AND ", predicates) : "";
        var combinedWhere = !string.IsNullOrEmpty(searchWhere) && !string.IsNullOrEmpty(tenantWhere)
            ? searchWhere + " AND " + tenantWhere.Substring(6)  // skip " WHERE "
            : (string.IsNullOrEmpty(searchWhere) ? tenantWhere : searchWhere);

        var sql = $"SELECT {quotedKey} AS value, {quotedDisplay} AS display FROM {quotedTable}{combinedWhere} ORDER BY {quotedDisplay} OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
        parameters.Add(new NpgsqlParameter("@Offset", (page - 1) * pageSize));
        parameters.Add(new NpgsqlParameter("@PageSize", pageSize));

        var items = (await connection.QueryAsync<dynamic>(sql, parameters.ToArray())).ToList();

        // Count total
        var countSql = $"SELECT COUNT(*) FROM {quotedTable}{combinedWhere}";
        var countParams = parameters.Where(p => p.ParameterName == "@Search" || p.ParameterName == "@ClientId" || p.ParameterName == "@OrgId").ToArray();
        var count = await connection.QuerySingleAsync<int>(countSql, countParams);

        return new
        {
            referenceName = reference.Name,
            targetTable = tableName,
            totalItems = count,
            pagination = new { page, pageSize, totalItems = count, totalPages = (int)Math.Ceiling(count / (double)pageSize) },
            items = items
        };
    }

    // ---- SEARCH reference with tenant/org isolation ----
    private static async Task<object> ResolveSearchReferenceWithSecurity(
        int page,
        int pageSize,
        string? search,
        SysReference reference,
        SysReferenceTable tableRef,
        string tableName,
        string keyColumnName,
        string displayColumnName,
        IReadOnlyContext context,
        Npgsql.NpgsqlConnection connection)
    {
        var quotedTable = $"\"{tableName}\"";
        var quotedKey = $"\"{keyColumnName}\"";
        var quotedDisplay = $"\"{displayColumnName}\"";

        var parameters = new List<NpgsqlParameter>();
        var predicates = new List<string>();

        // Search filter (parameterized)
        if (!string.IsNullOrWhiteSpace(search))
        {
            predicates.Add($"{quotedDisplay} ILIKE @Search");
            parameters.Add(new NpgsqlParameter("@Search", $"%{search}%"));
        }

        // Tenant isolation predicate
        if (!string.IsNullOrWhiteSpace(context.TenantPredicate))
        {
            predicates.Add(context.TenantPredicate!);
            if (!string.IsNullOrWhiteSpace(context.TenantId))
                parameters.Add(new NpgsqlParameter("@ClientId", context.TenantId!));
        }

        // Organization isolation predicate
        if (!string.IsNullOrWhiteSpace(context.OrgPredicate))
        {
            predicates.Add(context.OrgPredicate!);
            if (!string.IsNullOrWhiteSpace(context.OrgId))
                parameters.Add(new NpgsqlParameter("@OrgId", context.OrgId!));
        }

        var whereClause = predicates.Count > 0 ? " WHERE " + string.Join(" AND ", predicates) : "";

        var sql = $"SELECT {quotedKey} AS value, {quotedDisplay} AS display FROM {quotedTable}{whereClause} ORDER BY {quotedDisplay} OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
        parameters.Add(new NpgsqlParameter("@Offset", (page - 1) * pageSize));
        parameters.Add(new NpgsqlParameter("@PageSize", pageSize));

        var items = (await connection.QueryAsync<dynamic>(sql, parameters.ToArray())).ToList();

        var countSql = $"SELECT COUNT(*) FROM {quotedTable}{whereClause}";
        var countParams = parameters.Where(p => p.ParameterName == "@Search" || p.ParameterName == "@ClientId" || p.ParameterName == "@OrgId").ToArray();
        var count = await connection.QuerySingleAsync<int>(countSql, countParams);

        return new
        {
            referenceName = reference.Name,
            targetTable = tableName,
            totalItems = count,
            pagination = new { page, pageSize, totalItems = count, totalPages = (int)Math.Ceiling(count / (double)pageSize) },
            items = items
        };
    }
}
