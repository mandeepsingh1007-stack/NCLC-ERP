using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Platform.Core.Metadata;
using Platform.Core.Runtime;

namespace Platform.API.Endpoints;

/// <summary>
/// Generic Meta API — window metadata, window list, menu hierarchy.
/// Caches metadata in IMemoryCache + Redis.
/// </summary>
public static class MetaEndpoints
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public static IEndpointRouteBuilder MapGenericMetaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/meta");

        // GET /api/meta/window/{windowId}
        group.MapGet("/window/{windowId:int}", async (
            int windowId,
            IWindowMetadataBuilder builder,
            IDistributedCache cache,
            CancellationToken ct) =>
        {
            var cacheKey = $"window:{windowId}";
            var cached = await cache.GetStringAsync(cacheKey, ct);
            if (cached != null)
                return Results.Ok(System.Text.Json.JsonSerializer.Deserialize<dynamic>(cached));

            var contract = builder.BuildWindow(windowId);
            if (contract == null)
                return Results.NotFound(new { error = new { code = "WindowNotFound", message = $"Window with ID {windowId} not found." } });

            var json = System.Text.Json.JsonSerializer.Serialize(contract);
            await cache.SetStringAsync(cacheKey, json, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl }, ct);

            return Results.Ok(System.Text.Json.JsonSerializer.Deserialize<dynamic>(json));
        });

        // GET /api/meta/windows
        group.MapGet("/windows", async (
            IMetadataGraph metadataGraph,
            IDistributedCache cache,
            CancellationToken ct) =>
        {
            var cacheKey = "windows:list";
            var cached = await cache.GetStringAsync(cacheKey, ct);
            if (cached != null)
                return Results.Ok(System.Text.Json.JsonSerializer.Deserialize<dynamic>(cached));

            var windows = metadataGraph.GetWindows();
            var result = new
            {
                windows = windows.Select(w => new { w.SysWindowId, w.ColumnName, w.Name, w.Description })
            };

            var json = System.Text.Json.JsonSerializer.Serialize(result);
            await cache.SetStringAsync(cacheKey, json, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl }, ct);

            return Results.Ok(System.Text.Json.JsonSerializer.Deserialize<dynamic>(json));
        });

        // GET /api/meta/menu
        group.MapGet("/menu", async (
            IMetadataGraph metadataGraph,
            IDistributedCache cache,
            CancellationToken ct) =>
        {
            var cacheKey = "menu:root";
            var cached = await cache.GetStringAsync(cacheKey, ct);
            if (cached != null)
                return Results.Ok(System.Text.Json.JsonSerializer.Deserialize<dynamic>(cached));

            var menuItems = metadataGraph.GetMenuHierarchy();

            // Build hierarchy
            var itemsDict = new Dictionary<int, dynamic?>();
            var roots = new List<dynamic>();

            foreach (var item in menuItems.OrderBy(m => m.Sequence))
            {
                var node = new
                {
                    item.SysMenuId,
                    item.ColumnName,
                    item.Name,
                    item.Icon,
                    item.Sequence,
                    ParentId = item.ParentId,
                    item.WindowId,
                    item.ProcessId,
                    item.IsSeparator,
                    Children = (List<dynamic>)new List<dynamic>()
                };
                itemsDict[item.SysMenuId] = node;
            }

            // Suppress CS8602: dynamic types can't be statically analyzed for nullability
#pragma warning disable CS8602
            foreach (var item in menuItems.OrderBy(m => m.Sequence))
            {
                var node = itemsDict[item.SysMenuId];
                if (item.ParentId.HasValue)
                {
                    if (itemsDict.TryGetValue(item.ParentId.Value, out var parent) && parent.Children != null)
                    {
                        parent.Children.Add(node);
                    }
                    else
                    {
                        roots.Add(node);
                    }
                }
                else
                {
                    roots.Add(node);
                }
            }
#pragma warning restore CS8602

            var result = new { items = roots };
            var json = System.Text.Json.JsonSerializer.Serialize(result);
            await cache.SetStringAsync(cacheKey, json, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl }, ct);

            return Results.Ok(System.Text.Json.JsonSerializer.Deserialize<dynamic>(json));
        });

        return app;
    }
}
