using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using Platform.Core.Cache;
using Platform.Core.Metadata;
using Platform.Core.Runtime;

namespace Platform.Tests.Core.Runtime;

public class POLifecycleManagerTests
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
    public void Constructor_CreatesManager()
    {
        var mockGraph = new MockMetadataGraph();
        var validator = new POValidator(
            new TypeValidator(),
            new ReferenceValueValidator(mockGraph),
            new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>()),
            mockGraph);
        var metadataCache = new MetadataCacheService(
            new MemoryCache(new MemoryCacheOptions()),
            new DummyDistributedCache());
        var invalidation = new CacheInvalidationService(metadataCache, "localhost:6379");

        var manager = new POLifecycleManager(
            Array.Empty<IPOLifecycleHooks>(),
            validator,
            invalidation,
            mockGraph);

        manager.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAsync_RejectsInvalidPO()
    {
        var mockGraph = new MockMetadataGraph();
        // Add a mandatory column so validation fails on null
        mockGraph.AddColumn(new MetaColumn
        {
            TableName = "Users",
            ColumnName = "UserName",
            Label = "User Name",
            IsMandatory = true,
            IsActive = true
        });
        // Add a table so GetTableById returns something
        mockGraph.AddTable(new TableMetadata
        {
            SysTableId = 1,
            TableName = "Users"
        });

        var validator = new POValidator(
            new TypeValidator(),
            new ReferenceValueValidator(mockGraph),
            new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>()),
            mockGraph);
        var metadataCache = new MetadataCacheService(
            new MemoryCache(new MemoryCacheOptions()),
            new DummyDistributedCache());
        var invalidation = new CacheInvalidationService(metadataCache, "localhost:6379");

        var manager = new POLifecycleManager(
            Array.Empty<IPOLifecycleHooks>(),
            validator,
            invalidation,
            mockGraph);

        var po = new TestPO { SysTableId = 1, UserName = (string?)null };
        var context = InMemoryContext.Create("user1", "tenant1", "org1");

        var result = await manager.CreateAsync(po, context, _ => { });

        result.Allowed.Should().BeFalse();
        result.Message.Should().Contain("required");
    }

    [Fact]
    public async Task UpdateAsync_RejectsInvalidPO()
    {
        var mockGraph = new MockMetadataGraph();
        mockGraph.AddColumn(new MetaColumn
        {
            TableName = "Users",
            ColumnName = "UserName",
            Label = "User Name",
            IsMandatory = true,
            IsActive = true
        });
        mockGraph.AddTable(new TableMetadata
        {
            SysTableId = 1,
            TableName = "Users"
        });

        var validator = new POValidator(
            new TypeValidator(),
            new ReferenceValueValidator(mockGraph),
            new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>()),
            mockGraph);
        var metadataCache = new MetadataCacheService(
            new MemoryCache(new MemoryCacheOptions()),
            new DummyDistributedCache());
        var invalidation = new CacheInvalidationService(metadataCache, "localhost:6379");

        var manager = new POLifecycleManager(
            Array.Empty<IPOLifecycleHooks>(),
            validator,
            invalidation,
            mockGraph);

        var po = new TestPO { SysTableId = 1, UserName = (string?)null };
        var context = InMemoryContext.Create("user1", "tenant1", "org1");

        var result = await manager.UpdateAsync(po, new Dictionary<string, object?>(), context, () => { });

        result.Allowed.Should().BeFalse();
        result.Message.Should().Contain("required");
    }

    /// <summary>
    /// Minimal test PO that implements IPersistentObject.
    /// </summary>
    private class TestPO : IPersistentObject
    {
        public int SysTableId { get; set; }
        public string? UserName { get; set; }

        public void Load(int id, IReadOnlyContext context) { }
        public int Save(IReadOnlyContext context) => 0;
        public void Delete(IReadOnlyContext context) { }
    }
}
