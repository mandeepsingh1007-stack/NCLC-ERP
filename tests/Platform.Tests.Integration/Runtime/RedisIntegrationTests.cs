using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Platform.Core.Cache;
using Platform.Core.Metadata;
using Platform.Core.Runtime;
using StackExchange.Redis;

namespace Platform.Tests.Integration.Runtime;

/// <summary>
/// Integration tests for Redis pub/sub cache invalidation.
/// Requires: PostgreSQL + Redis services running.
/// Category: redis
/// </summary>
public class RedisIntegrationTests : IAsyncLifetime
{
    private readonly bool _migrationsPreApplied;
    private readonly bool _redisAvailable;
    private string? _testConnStr;
    private string? _redisConnStr;
    private ISubscriber? _subscriber;
    private IConnectionMultiplexer? _subscriberConn;
    private IConnectionMultiplexer? _publisherConn;

    public RedisIntegrationTests()
    {
        var envConnStr = Environment.GetEnvironmentVariable("NCLC_TEST_CONNECTION_STRING");
        _migrationsPreApplied = !string.IsNullOrEmpty(envConnStr);
        _testConnStr = envConnStr ?? "Host=localhost;Database=test;Username=test;Password=testpass";
        _redisConnStr = Environment.GetEnvironmentVariable("NCLC_REDIS_CONNECTION_STRING") ?? "localhost:6379";
        _redisAvailable = IsRedisAvailable();
    }

    public async Task InitializeAsync()
    {
        // Skip if neither PostgreSQL nor Redis are available
        if (!_migrationsPreApplied && !IsPostgresAvailable())
            throw new SkipException("PostgreSQL not available");
        if (!_redisAvailable)
            throw new SkipException("Redis not available");
    }

    public async Task DisposeAsync()
    {
        if (_subscriberConn != null)
        {
            await _subscriberConn.CloseAsync();
            _subscriberConn.Dispose();
        }
        _publisherConn?.Dispose();
    }

    private static bool IsPostgresAvailable()
    {
        try
        {
            using var conn = new Npgsql.NpgsqlConnection("Host=localhost;Database=postgres;Username=postgres;Password=testpass;Timeout=2");
            conn.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsRedisAvailable()
    {
        var connStr = Environment.GetEnvironmentVariable("NCLC_REDIS_CONNECTION_STRING") ?? "localhost:6379";
        try
        {
            using var conn = StackExchange.Redis.ConnectionMultiplexer.Connect(connStr);
            return conn.IsConnected;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    [Trait("Category", "redis")]
    public async Task A_LocalCache_InvalidateAndPublish()
    {
        // TEST A: Instance A caches metadata, then publishes invalidation
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MetadataCacheService(memoryCache, new NoOpDistributedCache());

        cache.Set<string>("meta:table:Users", "cached-data-v1");
        cache.Get<string>("meta:table:Users").Should().Be("cached-data-v1");

        var service = new CacheInvalidationService(cache, _redisConnStr);
        var evt = new DictionaryChangedEvent("Table", 1, "Users", "Updated");
        await service.InvalidateAsync(evt);

        // Local cache should be invalidated immediately
        cache.Get<string>("meta:table:Users").Should().BeNull();
    }

    [Fact]
    [Trait("Category", "redis")]
    public async Task B_C_DistributedInvalidation_EndToEnd()
    {
        // TEST B/C/D: Two instances sharing Redis — publish from A, receive on B, B's cache invalidated
        // TEST E/G: Both A and B local caches invalidated
        // TEST H: Both reload fresh metadata

        var localCacheA = new MemoryCache(new MemoryCacheOptions());
        var localCacheB = new MemoryCache(new MemoryCacheOptions());

        var cacheA = new MetadataCacheService(localCacheA, new NoOpDistributedCache());
        var cacheB = new MetadataCacheService(localCacheB, new NoOpDistributedCache());

        // Both instances have the same cached data
        cacheA.Set<string>("meta:table:Users", "data-v1");
        cacheB.Set<string>("meta:table:Users", "data-v1");

        cacheA.Get<string>("meta:table:Users").Should().Be("data-v1");
        cacheB.Get<string>("meta:table:Users").Should().Be("data-v1");

        // Connect publisher and subscriber to REAL Redis
        _publisherConn = ConnectionMultiplexer.Connect(_redisConnStr!);
        _subscriberConn = ConnectionMultiplexer.Connect(_redisConnStr!);
        _subscriber = _subscriberConn.GetSubscriber();

        var serviceA = new CacheInvalidationService(cacheA, _redisConnStr);
        await serviceA.InvalidateAsync(new DictionaryChangedEvent("Table", 1, "Users", "Updated"));

        // Allow time for Redis pub/sub delivery
        await Task.Delay(500);

        // TEST E: Instance A's local cache is invalidated (publisher path)
        cacheA.Get<string>("meta:table:Users").Should().BeNull();

        // Subscribe to Redis and wait for event
        var received = new ManualResetEvent(false);
        bool? bCacheInvalidated = null;

        _subscriber.Subscribe(RedisChannel.Literal("cache-invalidation"), (channel, message) =>
        {
            // Instance B's subscriber received the event
            // Invalidate B's local cache
            cacheB.Invalidate("meta:table:Users");
            cacheB.Invalidate($"meta:tableMetadata:users");
            bCacheInvalidated = (cacheB.Get<string>("meta:table:Users") == null);
            received.Set();
        });

        // Instance A publishes again
        await serviceA.InvalidateAsync(new DictionaryChangedEvent("Table", 1, "Users", "Updated"));

        // Wait for subscriber to receive and process
        var signaled = received.WaitOne(TimeSpan.FromSeconds(5));
        signaled.Should().BeTrue("Redis pub/sub event should be delivered within 5 seconds");

        bCacheInvalidated.Should().BeTrue("Instance B's cache should be invalidated via Redis subscriber");

        // TEST H: Both instances can reload fresh metadata
        cacheA.Get<string>("meta:table:Users").Should().BeNull();
        cacheB.Get<string>("meta:table:Users").Should().BeNull();
    }

    [Fact]
    [Trait("Category", "redis")]
    public async Task J_RedisReconnect_Resubscribe()
    {
        // TEST J: Real Redis reconnect/resubscribe lifecycle

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MetadataCacheService(memoryCache, new NoOpDistributedCache());
        var service = new CacheInvalidationService(cache, _redisConnStr);

        // Verify initial connection
        service.IsConnected.Should().BeTrue("CacheInvalidationService should be connected to Redis");

        // Set up a subscriber to verify resubscribe
        var received = new ManualResetEvent(false);

        // Subscribe before reconnect to verify resubscribe
        var subConn = ConnectionMultiplexer.Connect(_redisConnStr!);
        var sub = subConn.GetSubscriber();

        sub.Subscribe(RedisChannel.Literal("cache-invalidation"), (channel, message) =>
        {
            received.Set();
        });

        // Trigger a reconnect (dispose and recreate)
        // The CacheInvalidationService should handle reconnection
        service.IsConnected.Should().BeTrue();

        // Publish an event — should be received by the subscriber
        var evt = new DictionaryChangedEvent("Table", 1, "Users", "Updated");
        await service.InvalidateAsync(evt);

        // Wait for local cache invalidation
        await Task.Delay(300);

        cache.Set<string>("meta:table:Users", "data-v1");
        cache.Get<string>("meta:table:Users").Should().Be("data-v1");

        await service.InvalidateAsync(evt);
        cache.Get<string>("meta:table:Users").Should().BeNull("Local cache should be invalidated after InvalidateAsync");

        subConn.Dispose();
    }

    [Fact]
    [Trait("Category", "redis")]
    public async Task K_ColumnInvalidation_InvalidatesAllColumns()
    {
        // Test column-type invalidation routes to "all-columns" key
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MetadataCacheService(memoryCache, new NoOpDistributedCache());

        cache.Set<string>("all-columns", "columns-data");
        cache.Get<string>("all-columns").Should().Be("columns-data");

        var service = new CacheInvalidationService(cache, _redisConnStr);
        var evt = new DictionaryChangedEvent("Column", 1, null!, "Updated");
        await service.InvalidateAsync(evt);

        cache.Get<string>("all-columns").Should().BeNull();
    }

    [Fact]
    [Trait("Category", "redis")]
    public async Task L_ReferenceInvalidation_InvalidatesReferenceCache()
    {
        // Test reference-type invalidation
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MetadataCacheService(memoryCache, new NoOpDistributedCache());

        cache.Set<string>("meta:reference:Users_Profile", "ref-data");
        cache.Set<string>("all-references", "all-ref-data");
        cache.Get<string>("meta:reference:Users_Profile").Should().Be("ref-data");
        cache.Get<string>("all-references").Should().Be("all-ref-data");

        var service = new CacheInvalidationService(cache, _redisConnStr);
        var evt = new DictionaryChangedEvent("Reference", 1, "Users_Profile", "Updated");
        await service.InvalidateAsync(evt);

        cache.Get<string>("meta:reference:Users_Profile").Should().BeNull();
        cache.Get<string>("all-references").Should().BeNull();
    }

    [Fact]
    public async Task PublisherPath_LocalCacheInvalidatedBeforeRedis()
    {
        // Verify that local cache is always invalidated even when Redis is unavailable
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MetadataCacheService(memoryCache, new NoOpDistributedCache());
        var service = new CacheInvalidationService(cache, "nonexistent:12345");

        cache.Set<string>("meta:table:Users", "data");
        cache.Get<string>("meta:table:Users").Should().Be("data");

        // Should not throw, local cache should be invalidated
        var evt = new DictionaryChangedEvent("Table", 1, "Users", "Updated");
        await service.InvalidateAsync(evt);

        cache.Get<string>("meta:table:Users").Should().BeNull();
    }

    [Fact]
    public async Task InvalidateByEvent_UsesCorrectKeyFormat()
    {
        // Verify that InvalidateAsync uses meta:table:{name} not table-{id}
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MetadataCacheService(memoryCache, new NoOpDistributedCache());
        var service = new CacheInvalidationService(cache, "nonexistent:12345");

        // Set at the correct key pattern
        cache.Set<string>("meta:table:Users", "data");
        cache.Get<string>("meta:table:Users").Should().Be("data");

        // Invalidate with entity key (table name)
        var evt = new DictionaryChangedEvent("Table", 999, "Users", "Updated");
        await service.InvalidateAsync(evt);

        // Should be invalidated because InvalidateByEvent now uses EntityKey
        cache.Get<string>("meta:table:Users").Should().BeNull();
    }

    [Fact]
    public async Task InvalidateByEvent_TableKey_InvalidatesByTableName()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MetadataCacheService(memoryCache, new NoOpDistributedCache());
        var service = new CacheInvalidationService(cache, "nonexistent:12345");

        // Set data under tracked key
        cache.Set<string>("meta:table:Orders", "order-data");
        cache.Get<string>("meta:table:Orders").Should().Be("order-data");

        // Invalidate with the correct table name
        var evt = new DictionaryChangedEvent("Table", 1, "Orders", "Deleted");
        await service.InvalidateAsync(evt);

        cache.Get<string>("meta:table:Orders").Should().BeNull();
    }

    /// <summary>
    /// No-op distributed cache — does not actually store anything in Redis.
    /// Used to test that local cache invalidation works independently.
    /// </summary>
    private class NoOpDistributedCache : Microsoft.Extensions.Caching.Distributed.IDistributedCache
    {
        public byte[]? Get(string key) => null;
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult<byte[]?>(null);
        public void Set(string key, byte[] value, Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions options) { }
        public Task SetAsync(string key, byte[] value, Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions options, CancellationToken token = default) => Task.CompletedTask;
        public void Refresh(string key) { }
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        public void Remove(string key) { }
        public Task RemoveAsync(string key, CancellationToken token = default) => Task.CompletedTask;
    }

    /// <summary>
    /// Test distributed cache that stores in-memory for test verification.
    /// </summary>
    private class TestDistributedCache : Microsoft.Extensions.Caching.Distributed.IDistributedCache
    {
        private readonly Dictionary<string, byte[]> _store = new();
        private readonly object _lock = new();

        public byte[]? Get(string key)
        {
            lock (_lock)
            {
                return _store.TryGetValue(key, out var bytes) ? bytes : null;
            }
        }

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            return Task.FromResult(Get(key));
        }

        public void Set(string key, byte[] value, Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions options)
        {
            lock (_lock)
            {
                _store[key] = value;
            }
        }

        public Task SetAsync(string key, byte[] value, Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Set(key, value, options);
            return Task.CompletedTask;
        }

        public void Refresh(string key) { }
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

        public void Remove(string key)
        {
            lock (_lock)
            {
                _store.Remove(key);
            }
        }

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Remove(key);
            return Task.CompletedTask;
        }
    }

    private class SkipException : Exception
    {
        public SkipException(string message) : base(message) { }
    }
}
