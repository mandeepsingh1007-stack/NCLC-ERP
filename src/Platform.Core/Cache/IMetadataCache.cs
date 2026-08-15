namespace Platform.Core.Cache;

/// <summary>
/// Two-layer metadata cache: node-local IMemoryCache + distributed Redis.
/// All writes go to local first. Invalidation publishes to Redis pub/sub.
/// </summary>
public interface IMetadataCache
{
    T? Get<T>(string key);
    void Set<T>(string key, T value, TimeSpan? ttl = null);
    void Invalidate(string key);
    void InvalidateTable(string tableName);
    Task<(bool Found, T? Value)> TryGetValueAsync<T>(string key);
    IReadOnlyCollection<string> GetAllKeys();
}
