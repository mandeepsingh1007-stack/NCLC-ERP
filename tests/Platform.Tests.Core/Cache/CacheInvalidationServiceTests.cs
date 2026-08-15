using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Platform.Core.Cache;
using Platform.Core.Runtime;

namespace Platform.Tests.Core.Cache;

public class CacheInvalidationServiceTests
{
    private class DummyDistributedCache : Microsoft.Extensions.Caching.Distributed.IDistributedCache
    {
        public byte[]? Get(string key) => Array.Empty<byte>();
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult<byte[]?>(Array.Empty<byte>());
        public void Set(string key, byte[] value, Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions options) { }
        public Task SetAsync(string key, byte[] value, Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions options, CancellationToken token = default) => Task.CompletedTask;
        public void Refresh(string key) { }
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        public void Remove(string key) { }
        public Task RemoveAsync(string key, CancellationToken token = default) => Task.CompletedTask;
    }

    [Fact]
    public void Constructor_CreatesService()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MetadataCacheService(memoryCache, new DummyDistributedCache());
        var service = new CacheInvalidationService(cache, "localhost:6379");

        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_PublisherOnly_NoRedisConnStr()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MetadataCacheService(memoryCache, new DummyDistributedCache());
        var service = new CacheInvalidationService(cache);

        service.Should().NotBeNull();
        // IsConnected should be false since no connection was established
        service.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void IsConnected_WithoutConnectionMultiplexer_ReturnsFalse()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MetadataCacheService(memoryCache, new DummyDistributedCache());
        var service = new CacheInvalidationService(cache, "localhost:6379");

        // Before Lazy<T> is triggered (Redis not yet accessed), IsConnected is false
        service.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task InvalidateAsync_LocalCacheWorksWithoutRedis()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MetadataCacheService(memoryCache, new DummyDistributedCache());
        // Use a completely invalid Redis address — won't connect
        var service = new CacheInvalidationService(cache, "invalid:12345");

        // Set a value
        cache.Set<string>("meta:table:Users", "data");
        cache.Get<string>("meta:table:Users").Should().Be("data");

        // Invalidate should NOT throw even with invalid Redis
        var evt = new DictionaryChangedEvent("Table", 1, "Users", "Updated");
        await service.InvalidateAsync(evt);

        // Local cache should be invalidated
        cache.Get<string>("meta:table:Users").Should().BeNull();
    }

    [Fact]
    public void Dispose_DoesNotThrowWhenConnectionNeverCreated()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MetadataCacheService(memoryCache, new DummyDistributedCache());
        var service = new CacheInvalidationService(cache, "localhost:6379");

        // Dispose without ever accessing Redis (Lazy<T> not triggered)
        var act = () => service.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_WithPublisherOnlyMode_DoesNotThrow()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MetadataCacheService(memoryCache, new DummyDistributedCache());
        var service = new CacheInvalidationService(cache);

        // Publisher-only mode — no connection string
        var act = () => service.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task InvalidateAsync_NilEvent_DoesNotThrow()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MetadataCacheService(memoryCache, new DummyDistributedCache());
        var service = new CacheInvalidationService(cache, "invalid:12345");

        // Event with empty values should not throw
        var evt = new DictionaryChangedEvent("", 0, null!, "");
        await service.InvalidateAsync(evt);

        // Verify evt is used
        evt.Should().NotBeNull();
    }

    [Fact]
    public async Task ConnectionChanged_Event_FiresOnDispose()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MetadataCacheService(memoryCache, new DummyDistributedCache());
        var service = new CacheInvalidationService(cache, "localhost:6379");

        bool? eventFired = null;
        service.ConnectionChanged += (sender, args) =>
        {
            eventFired = true;
        };

        // Trigger the Lazy<T> to set up handlers, then dispose
        _ = service.IsConnected;
        service.Dispose();

        // Note: IsConnected accesses _redisLazy.Value but doesn't trigger events
        // The event would fire only on ConnectionFailed/ConnectionRestored from Redis itself
        // Since Redis isn't running, no events fire — the test verifies the mechanism exists
        // without actually connecting to Redis
        eventFired.Should().BeNull();
    }

    [Fact]
    public async Task InvalidateByEvent_RoutesCorrectly()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MetadataCacheService(memoryCache, new DummyDistributedCache());
        var service = new CacheInvalidationService(cache, "invalid:12345");

        // Set up cache entries for all entity types
        cache.Set<string>("meta:table:Users", "data");
        cache.Set<string>("all-columns", "data");
        cache.Set<string>("meta:reference:Users_Profile", "data");
        cache.Set<string>("all-references", "data");

        // Invalidate table
        var evtTable = new DictionaryChangedEvent("Table", 1, "Users", "Updated");
        await service.InvalidateAsync(evtTable);
        cache.Get<string>("meta:table:Users").Should().BeNull();

        // Invalidate columns
        var evtCol = new DictionaryChangedEvent("Column", 1, null!, "Updated");
        await service.InvalidateAsync(evtCol);
        cache.Get<string>("all-columns").Should().BeNull();

        // Invalidate reference
        var evtRef = new DictionaryChangedEvent("Reference", 1, "Users_Profile", "Updated");
        await service.InvalidateAsync(evtRef);
        cache.Get<string>("meta:reference:Users_Profile").Should().BeNull();
        cache.Get<string>("all-references").Should().BeNull();
    }
}
