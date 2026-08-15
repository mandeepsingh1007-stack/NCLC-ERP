using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using Platform.Core.Cache;
using Platform.Core.Runtime;

namespace Platform.Tests.Core.Runtime;

public class MetadataCacheServiceTests
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

    private readonly MetadataCacheService _sut;
    private readonly IMemoryCache _memoryCache;

    public MetadataCacheServiceTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _sut = new MetadataCacheService(_memoryCache, new DummyDistributedCache());
    }

    [Fact]
    public void SetAndGet_SimpleValue()
    {
        _sut.Set<string>("test-key", "test-value");

        var result = _sut.Get<string>("test-key");
        result.Should().Be("test-value");
    }

    [Fact]
    public void Set_DefaultValue_DoesNotCache()
    {
        _sut.Set<string>("null-key", default!);

        var result = _sut.Get<string>("null-key");
        result.Should().BeNull();
    }

    [Fact]
    public void Get_MissingKey_ReturnsDefault()
    {
        var result = _sut.Get<string>("nonexistent-key");

        result.Should().BeNull();
    }

    [Fact]
    public void Invalidate_RemovesFromCache()
    {
        _sut.Set<string>("invalidate-key", "test-value");
        _sut.Invalidate("invalidate-key");

        var result = _sut.Get<string>("invalidate-key");
        result.Should().BeNull();
    }

    [Fact]
    public void GetAllKeys_TracksCachedKeys()
    {
        _sut.Set<string>("key-1", "value-1");
        _sut.Set<string>("key-2", "value-2");

        var keys = _sut.GetAllKeys();
        keys.Should().Contain("key-1");
        keys.Should().Contain("key-2");
    }

    [Fact]
    public void InvalidateTable_RemovesMatchingKeys()
    {
        _sut.Set<string>("meta:table:users-1", "table-data");
        _sut.Set<string>("meta:table:orders-1", "other-data");
        _sut.Set<string>("other-key", "unrelated");

        _sut.InvalidateTable("users");

        var keys = _sut.GetAllKeys();
        keys.Should().NotContain("meta:table:users-1");
        keys.Should().Contain("meta:table:orders-1");
        keys.Should().Contain("other-key");
    }
}
