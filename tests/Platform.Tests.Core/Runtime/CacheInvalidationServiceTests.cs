using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using Platform.Core.Cache;
using Platform.Core.Runtime;

namespace Platform.Tests.Core.Runtime;

public class CacheInvalidationServiceTests
{
    private class DummyDistributedCache : IDistributedCache
    {
        public byte[]? Get(string key) => Array.Empty<byte>();
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult<byte[]?>(Array.Empty<byte>());
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) { }
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => Task.CompletedTask;
        public void Refresh(string key) { }
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        public void Remove(string key) { }
        public Task RemoveAsync(string key, CancellationToken token = default) => Task.CompletedTask;
    }

    [Fact]
    public void Constructor_CreatesService()
    {
        var localCache = new MemoryCache(new MemoryCacheOptions());
        var redisCache = new DummyDistributedCache();
        var metadataCache = new MetadataCacheService(localCache, redisCache);
        var service = new CacheInvalidationService(metadataCache, "localhost:6379");

        service.Should().NotBeNull();
    }

    [Fact]
    public async Task InvalidateAsync_ThrowsNoException()
    {
        var localCache = new MemoryCache(new MemoryCacheOptions());
        var redisCache = new DummyDistributedCache();
        var metadataCache = new MetadataCacheService(localCache, redisCache);
        var service = new CacheInvalidationService(metadataCache, "localhost:6379");

        var evt = new DictionaryChangedEvent("Table", 1, "Users", "Created");

        // Should not throw even if Redis is unavailable
        await service.InvalidateAsync(evt);
    }

    [Fact]
    public async Task InvalidateAsync_RemovesLocalCache_ForTable()
    {
        var localCache = new MemoryCache(new MemoryCacheOptions());
        var redisCache = new DummyDistributedCache();
        var metadataCache = new MetadataCacheService(localCache, redisCache);
        var service = new CacheInvalidationService(metadataCache, "localhost:6379");

        // Set a value in cache
        metadataCache.Set<string>("meta:table:Users", "data");
        metadataCache.Get<string>("meta:table:Users").Should().Be("data");

        // Invalidate via the service
        var evt = new DictionaryChangedEvent("Table", 1, "Users", "Updated");
        await service.InvalidateAsync(evt);

        // Local cache should be invalidated by the publisher path
        metadataCache.Get<string>("meta:table:Users").Should().BeNull();
    }

    [Fact]
    public async Task InvalidateTableAsync_RemovesLocalCache()
    {
        var localCache = new MemoryCache(new MemoryCacheOptions());
        var redisCache = new DummyDistributedCache();
        var metadataCache = new MetadataCacheService(localCache, redisCache);
        var service = new CacheInvalidationService(metadataCache, "localhost:6379");

        metadataCache.Set<string>("meta:table:Users", "data");
        metadataCache.Get<string>("meta:table:Users").Should().Be("data");

        await service.InvalidateTableAsync("Users");

        metadataCache.Get<string>("meta:table:Users").Should().BeNull();
    }

    [Fact]
    public void Dispose_CallsDispose()
    {
        var localCache = new MemoryCache(new MemoryCacheOptions());
        var redisCache = new DummyDistributedCache();
        var metadataCache = new MetadataCacheService(localCache, redisCache);
        var service = new CacheInvalidationService(metadataCache, "localhost:6379");

        var act = () => service.Dispose();
        act.Should().NotThrow();
    }
}
