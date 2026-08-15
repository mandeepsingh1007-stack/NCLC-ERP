using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Npgsql;
using Platform.Core.Metadata;
using Platform.Core.Runtime;
using Platform.Data.Repositories;

namespace Platform.API.Endpoints;

/// <summary>
/// Generic Lookup API — resolves reference data for lookups (LIST, TABLE, SEARCH).
/// Caches lookups in IMemoryCache + Redis.
/// </summary>
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
            int page,
            int pageSize,
            string? search,
            SysReferenceRepository referenceRepo,
            SysReferenceListRepository referenceListRepo,
            SysReferenceTableRepository referenceTableRepo,
            SysTableRepository sysTableRepo,
            SysColumnRepository sysColumnRepo,
            Npgsql.NpgsqlConnection connection,
            IDistributedCache cache) =>
        {
            // Validate reference exists (sync method)
            var reference = referenceRepo.GetById(referenceId);
            if (reference == null)
                return Results.NotFound(new { error = new { code = "ReferenceNotFound", message = $"Reference with ID {referenceId} not found." } });

            var cacheKey = $"lookup:{referenceId}:{page}:{pageSize}:{search ?? ""}";
            var cached = await cache.GetStringAsync(cacheKey);
            if (cached != null)
                return Results.Ok(System.Text.Json.JsonSerializer.Deserialize<dynamic>(cached));

            pageSize = Math.Min(pageSize, MaxLookupPageSize);
            if (pageSize < 1) pageSize = 50;

            var validationType = reference.ValidationType;

            switch (validationType)
            {
                case ValidationTypeEnum.List:
                    {
                        var result = ResolveListReference(referenceId, page, pageSize, reference, referenceListRepo);
                        var json = System.Text.Json.JsonSerializer.Serialize(result);
                        await cache.SetStringAsync(cacheKey, json, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl });
                        return Results.Ok(System.Text.Json.JsonSerializer.Deserialize<dynamic>(json));
                    }

                case ValidationTypeEnum.Table:
                    {
                        var result = await ResolveTableReference(referenceId, page, pageSize, search, reference, referenceTableRepo, sysTableRepo, sysColumnRepo, connection);
                        if (result is IResult result1)
                            return result1;

                        var json = System.Text.Json.JsonSerializer.Serialize(result);
                        await cache.SetStringAsync(cacheKey, json, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl });
                        return Results.Ok(System.Text.Json.JsonSerializer.Deserialize<dynamic>(json));
                    }

                case ValidationTypeEnum.Search:
                    {
                        var result = await ResolveSearchReference(referenceId, page, pageSize, search, reference, referenceTableRepo, sysTableRepo, sysColumnRepo, connection);
                        if (result is IResult result2)
                            return result2;

                        var json = System.Text.Json.JsonSerializer.Serialize(result);
                        await cache.SetStringAsync(cacheKey, json, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl });
                        return Results.Ok(System.Text.Json.JsonSerializer.Deserialize<dynamic>(json));
                    }

                default:
                    return Results.BadRequest(new { error = new { code = "InvalidReferenceType", message = $"Unknown validation type: {reference.ValidationType}" } });
            }
        });

        return app;
    }

    private static object ResolveListReference(
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
            totalItems = totalCount,
            pagination = new { page, pageSize, totalItems = totalCount, totalPages = (int)Math.Ceiling(totalCount / (double)pageSize) },
            items = paged.Select(i => new
            {
                value = i.Value,
                display = i.Name
            })
        };
    }

    private static async Task<object> ResolveTableReference(
        int referenceId,
        int page,
        int pageSize,
        string? search,
        SysReference reference,
        SysReferenceTableRepository referenceTableRepo,
        SysTableRepository sysTableRepo,
        SysColumnRepository sysColumnRepo,
        Npgsql.NpgsqlConnection connection)
    {
        var tableRefs = referenceTableRepo.GetByReferenceId(referenceId).ToList();
        if (!tableRefs.Any())
            return Results.BadRequest(new { error = new { code = "NoReferenceTable", message = $"No target table configured for reference {referenceId}." } });
        var tableRef = tableRefs.First();

        var sysTable = sysTableRepo.GetById(tableRef.SysTableId);
        if (sysTable == null)
            return Results.BadRequest(new { error = new { code = "TableNotFound", message = $"Target table {tableRef.SysTableId} not found." } });

        var quotedTable = $"\"{sysTable.TableName}\"";

        var columns = sysColumnRepo.GetByTableId(sysTable.SysTableId);
        var keyCol = columns.FirstOrDefault(c => c.ColumnName == tableRef.KeyColumn);
        var displayCol = columns.FirstOrDefault(c => c.ColumnName == tableRef.DisplayColumn);

        if (keyCol == null || displayCol == null)
            return Results.BadRequest(new { error = new { code = "InvalidFieldMapping", message = $"KeyColumn '{tableRef.KeyColumn}' or DisplayColumn '{tableRef.DisplayColumn}' not found." } });

        // Build query with optional search filter
        var parameters = new List<NpgsqlParameter>();
        var whereClause = string.Empty;

        if (!string.IsNullOrWhiteSpace(search))
        {
            whereClause = $" WHERE \"{displayCol.ColumnName}\" ILIKE @Search";
            parameters.Add(new NpgsqlParameter("@Search", $"%{search}%"));
        }

        var sql = $"SELECT \"{keyCol.ColumnName}\" AS value, \"{displayCol.ColumnName}\" AS display FROM {quotedTable}{whereClause} ORDER BY \"{displayCol.ColumnName}\" OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
        parameters.Add(new NpgsqlParameter("@Offset", (page - 1) * pageSize));
        parameters.Add(new NpgsqlParameter("@PageSize", pageSize));

        var items = (await connection.QueryAsync<dynamic>(sql, parameters.ToArray())).ToList();

        // Count total
        var countSql = $"SELECT COUNT(*) FROM {quotedTable}{whereClause}";
        var countParams = parameters.Where(p => p.ParameterName == "@Search").ToArray();
        var count = await connection.QuerySingleAsync<int>(countSql, countParams);

        return new
        {
            referenceName = reference.Name,
            targetTable = sysTable.TableName,
            totalItems = count,
            pagination = new { page, pageSize, totalItems = count, totalPages = (int)Math.Ceiling(count / (double)pageSize) },
            items = items
        };
    }

    private static async Task<object> ResolveSearchReference(
        int referenceId,
        int page,
        int pageSize,
        string? search,
        SysReference reference,
        SysReferenceTableRepository referenceTableRepo,
        SysTableRepository sysTableRepo,
        SysColumnRepository sysColumnRepo,
        Npgsql.NpgsqlConnection connection)
    {
        var tableRefs = referenceTableRepo.GetByReferenceId(referenceId).ToList();
        if (!tableRefs.Any())
            return Results.BadRequest(new { error = new { code = "NoReferenceTable", message = $"No target table configured for reference {referenceId}." } });
        var tableRef = tableRefs.First();

        var sysTable = sysTableRepo.GetById(tableRef.SysTableId);
        if (sysTable == null)
            return Results.BadRequest(new { error = new { code = "TableNotFound", message = $"Target table {tableRef.SysTableId} not found." } });

        var quotedTable = $"\"{sysTable.TableName}\"";

        var columns = sysColumnRepo.GetByTableId(sysTable.SysTableId);
        var keyCol = columns.FirstOrDefault(c => c.ColumnName == tableRef.KeyColumn);
        var displayCol = columns.FirstOrDefault(c => c.ColumnName == tableRef.DisplayColumn);

        if (keyCol == null || displayCol == null)
            return Results.BadRequest(new { error = new { code = "InvalidFieldMapping", message = $"KeyColumn '{tableRef.KeyColumn}' or DisplayColumn '{tableRef.DisplayColumn}' not found." } });

        var whereClauses = new List<string>();
        var parameters = new List<NpgsqlParameter>();

        if (!string.IsNullOrWhiteSpace(search))
        {
            whereClauses.Add($"\"{displayCol.ColumnName}\" ILIKE @Search");
            parameters.Add(new NpgsqlParameter("@Search", $"%{search}%"));
        }

        var whereClause = whereClauses.Count > 0 ? " WHERE " + string.Join(" AND ", whereClauses) : "";

        var sql = $"SELECT \"{keyCol.ColumnName}\" AS value, \"{displayCol.ColumnName}\" AS display FROM {quotedTable}{whereClause} ORDER BY \"{displayCol.ColumnName}\" OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
        parameters.Add(new NpgsqlParameter("@Offset", (page - 1) * pageSize));
        parameters.Add(new NpgsqlParameter("@PageSize", pageSize));

        var items = (await connection.QueryAsync<dynamic>(sql, parameters.ToArray())).ToList();

        var countSql = $"SELECT COUNT(*) FROM {quotedTable}{whereClause}";
        var countParams = parameters.Where(p => p.ParameterName == "@Search").ToArray();
        var count = await connection.QuerySingleAsync<int>(countSql, countParams);

        return new
        {
            referenceName = reference.Name,
            targetTable = sysTable.TableName,
            totalItems = count,
            pagination = new { page, pageSize, totalItems = count, totalPages = (int)Math.Ceiling(count / (double)pageSize) },
            items = items
        };
    }
}
