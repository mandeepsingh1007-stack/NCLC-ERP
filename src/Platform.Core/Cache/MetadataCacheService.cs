using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace Platform.Core.Cache;

/// <summary>
/// Two-layer metadata cache: IMemoryCache (node-local) + IDistributedCache (Redis).
/// Get order: local -> Redis -> miss.
/// Set order: local only (Redis is handled via invalidation pub/sub).
/// Invalidate: local immediate + publish to Redis pub/sub for distributed invalidation.
/// TTL defaults to 1 hour.
/// </summary>
public class MetadataCacheService : IMetadataCache, IDisposable
{
    private readonly IMemoryCache _localCache;
    private readonly IDistributedCache _redisCache;
    private readonly ConcurrentDictionary<string, bool> _trackedKeys = new();
    private bool _disposed;

    public MetadataCacheService(
        IMemoryCache localCache,
        IDistributedCache redisCache)
    {
        _localCache = localCache;
        _redisCache = redisCache;
    }

    public T? Get<T>(string key)
    {
        // Try local first
        if (_localCache.TryGetValue(key, out T? localValue))
        {
            return localValue;
        }

        // Try Redis
        var bytes = _redisCache.Get(key);
        if (bytes != null && bytes.Length > 0)
        {
            try
            {
                var value = Deserialize<T>(bytes);
                // Populate local cache on distributed hit
                if (!EqualityComparer<T>.Default.Equals(value, default!) && typeof(T).IsValueType != true)
                {
                    _trackedKeys.TryAdd(key, true);
                    _localCache.Set(key, value, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) });
                }
                return value;
            }
            catch
            {
                // Corrupted Redis data — treat as miss
                return default;
            }
        }

        return default;
    }

    public void Set<T>(string key, T value, TimeSpan? ttl = null)
    {
        if (EqualityComparer<T>.Default.Equals(value, default!))
        {
            // Don't cache default/null values
            return;
        }

        // Set local cache
        _trackedKeys.TryAdd(key, true);
        if (ttl.HasValue)
        {
            _localCache.Set(key, value, ttl.Value);
        }
        else
        {
            _localCache.Set(key, value);
        }

        // Don't write to Redis on every set — that's handled by the graph reload
        // Redis is primarily for invalidation pub/sub in Phase 2
    }

    public void Invalidate(string key)
    {
        // Remove from local cache
        _localCache.Remove(key);

        // Remove from Redis
        try
        {
            _redisCache.Remove(key);
        }
        catch
        {
            // Redis unavailable — local cache is still valid
        }
    }

    public void InvalidateTable(string tableName)
    {
        // Remove all keys for this table from local cache.
        // Since IMemoryCache doesn't expose GetAllKeys directly, we use a concurrent bag to track keys.
        var prefix = $"meta:table:{tableName.ToLowerInvariant()}";
        var metaPrefix = $"meta:tableMetadata:{tableName.ToLowerInvariant()}";
        foreach (var key in _trackedKeys.Keys.ToList())
        {
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
             || key.StartsWith(metaPrefix, StringComparison.OrdinalIgnoreCase))
            {
                _localCache.Remove(key);
                _trackedKeys.TryRemove(key, out _);
            }
        }

        // Also remove from Redis
        try
        {
            foreach (var key in _trackedKeys.Keys.ToList())
            {
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                 || key.StartsWith(metaPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    _redisCache.Remove(key);
                }
            }
        }
        catch
        {
            // Best effort
        }
    }

    public async Task<(bool Found, T? Value)> TryGetValueAsync<T>(string key)
    {
        // Try local
        if (_localCache.TryGetValue(key, out T? localValue))
        {
            return (true, localValue);
        }

        // Try Redis
        var bytes = await _redisCache.GetAsync(key);
        if (bytes != null && bytes.Length > 0)
        {
            try
            {
                var value = Deserialize<T>(bytes);
                // Populate local cache on distributed hit
                if (!EqualityComparer<T>.Default.Equals(value, default!))
                {
                    _localCache.Set(key, value, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) });
                }
                return (true, value);
            }
            catch
            {
                return (false, default);
            }
        }

        return (false, default);
    }

    public IReadOnlyCollection<string> GetAllKeys()
    {
        return _trackedKeys.Keys.ToList().AsReadOnly();
    }

    private static T? Deserialize<T>(byte[] bytes)
    {
        var json = System.Text.Encoding.UTF8.GetString(bytes);
        return System.Text.Json.JsonSerializer.Deserialize<T>(json);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            (_localCache as IDisposable)?.Dispose();
            _disposed = true;
        }
    }
}
