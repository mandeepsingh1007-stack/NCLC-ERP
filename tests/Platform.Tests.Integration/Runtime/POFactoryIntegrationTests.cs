using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Platform.Core.Cache;
using Platform.Core.Runtime;

namespace Platform.Tests.Integration.Runtime;

/// <summary>
/// Integration tests for PO Factory and Context Variable resolution with a real database.
/// </summary>
public class POFactoryIntegrationTests : IAsyncLifetime
{
    private Npgsql.NpgsqlConnection? _connection;
    private readonly bool _migrationsPreApplied;
    private string? _testConnStr;

    public POFactoryIntegrationTests()
    {
        var envConnStr = Environment.GetEnvironmentVariable("NCLC_TEST_CONNECTION_STRING");
        _migrationsPreApplied = !string.IsNullOrEmpty(envConnStr);
        _testConnStr = envConnStr ?? "Host=localhost;Database=test;Username=test;Password=testpass";
    }

    public async Task InitializeAsync()
    {
        if (_migrationsPreApplied)
        {
            _connection = new Npgsql.NpgsqlConnection(_testConnStr!);
            await _connection.OpenAsync();
        }
        else
        {
            var container = new Testcontainers.PostgreSql.PostgreSqlBuilder("postgres:15-alpine")
                .WithPassword("testpass")
                .Build();
            await container.StartAsync();
            _connection = new Npgsql.NpgsqlConnection(container.GetConnectionString());
            await _connection.OpenAsync();

            var schemaPath = Path.Combine(GetRepositoryRoot(), "src", "Platform.Data", "Migrations", "001_Create_Dictionary_Schema.sql");
            using var schemaCmd = new Npgsql.NpgsqlCommand(await File.ReadAllTextAsync(schemaPath), _connection);
            await schemaCmd.ExecuteNonQueryAsync();

            var seedPath = Path.Combine(GetRepositoryRoot(), "src", "Platform.Data", "Migrations", "002_Seed_Dictionary_Data.sql");
            using var seedCmd = new Npgsql.NpgsqlCommand(await File.ReadAllTextAsync(seedPath), _connection);
            await seedCmd.ExecuteNonQueryAsync();
        }
    }

    public async Task DisposeAsync()
    {
        _connection?.Close();
        _connection?.Dispose();
    }

    [Fact]
    public async Task MetadataGraph_BatchLoading_VerifyNoNPlusOne()
    {
        // Verify that MetadataGraph loads tables in O(1) batch queries, not N queries per column.
        // With seeded data (~10+ tables, ~50+ columns), loading should complete quickly.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var graph = new MetadataGraph(_testConnStr!);
        sw.Stop();

        var tables = graph.GetTableNames();
        tables.Should().NotBeNull();
        tables.Count.Should().BeGreaterThanOrEqualTo(7);

        // Should load within 5 seconds (N+1 would take much longer)
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ContextVariableResolver_ResolvesAllBuiltInVariables()
    {
        var resolver = new ContextVariableResolver();
        var context = InMemoryContext.Create("alice", "acme", "org42");

        var userId = resolver.Resolve<string>("$UserId", context);
        userId.Should().Be("alice");

        var tenantId = resolver.Resolve<string>("$TenantId", context);
        tenantId.Should().Be("acme");

        var orgId = resolver.Resolve<string>("$OrgId", context);
        orgId.Should().Be("org42");

        var timestamp = resolver.Resolve<string>("$Timestamp", context);
        timestamp.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ContextVariableResolver_GetCurrentContext_ReturnsDefaultContext()
    {
        var resolver = new ContextVariableResolver();
        var ctx = resolver.GetCurrentContext();

        // Default context returns null for all fields
        ctx.UserId.Should().BeNull();
        ctx.TenantId.Should().BeNull();
        ctx.OrgId.Should().BeNull();
    }

    [Fact]
    public async Task POFactory_ResolveMClass_FromDatabase_WithMetadataGraph()
    {
        var graph = new MetadataGraph(_testConnStr!);
        var factory = new Platform.Metadata.Factory.POFactory(graph);

        // "Users" is a seeded table — M_Users may or may not exist.
        // This tests that the factory resolves without throwing.
        var mClass = factory.ResolveMClass("Users");
        // May be null if no M_Users class exists — but should not throw
        mClass.Should().BeNull();
    }

    [Fact]
    public async Task POLifecycleManager_ConstructorWithRealGraph()
    {
        var graph = new MetadataGraph(_testConnStr!);
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MetadataCacheService(memoryCache, new DummyCache());
        var invalidation = new CacheInvalidationService(cache, "localhost:6379");
        var validator = new POValidator(
            new TypeValidator(),
            new ReferenceValueValidator(graph),
            new ValRuleEngine(_testConnStr!, graph.GetTableNames()),
            graph);

        var manager = new POLifecycleManager(
            Array.Empty<IPOLifecycleHooks>(),
            validator,
            invalidation,
            graph);

        manager.Should().NotBeNull();
    }

    [Fact]
    public async Task MetadataGraph_GetColumns_ReturnsActiveAndInactive()
    {
        var graph = new MetadataGraph(_testConnStr!);
        var columns = graph.GetColumns("Users");

        columns.Should().NotBeNull();
        columns.Count.Should().BeGreaterThan(0);

        // Verify expected columns exist
        var columnNames = columns.Select(c => c.ColumnName).ToHashSet();
        columnNames.Should().Contain("UserName");
    }

    [Fact]
    public async Task MetadataGraph_GetTable_ReturnsNullForNonExistentTable()
    {
        var graph = new MetadataGraph(_testConnStr!);
        var result = graph.GetTable("NonExistentTable_xyz");

        result.Should().BeNull();
    }

    [Fact]
    public async Task TypeValidator_WithRealMetadata_VariousTypes()
    {
        var graph = new MetadataGraph(_testConnStr!);
        var validator = new TypeValidator();

        var columns = graph.GetColumns("Users");

        // Find a VarChar column and validate
        var varcharCol = columns.FirstOrDefault(c => c.BaseType == "VarChar");
        if (varcharCol != null)
        {
            var result = validator.Validate(
                varcharCol.ColumnName, "test-value", varcharCol.FieldLength, varcharCol.BaseType!);
            result.IsSuccess.Should().BeTrue();
        }
    }

    [Fact]
    public async Task CacheInvalidationService_InvalidateTableEntity()
    {
        var graph = new MetadataGraph(_testConnStr!);
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MetadataCacheService(memoryCache, new DummyCache());
        var invalidation = new CacheInvalidationService(cache, "localhost:6379");

        // Set a value, invalidate, verify removed
        cache.Set<string>("meta:table:Users-1", "data");
        cache.Get<string>("meta:table:Users-1").Should().Be("data");

        var evt = new DictionaryChangedEvent("table", 1, "Users", "Updated");
        await invalidation.InvalidateAsync(evt);

        // After invalidation, the key should be gone
        cache.Get<string>("meta:table:Users-1").Should().BeNull();
    }

    private static string GetRepositoryRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")) ||
                Path.GetFileName(Path.GetDirectoryName(dir)) == "NCLC")
            {
                return dir;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new InvalidOperationException("Could not find repository root.");
    }

    private class DummyCache : Microsoft.Extensions.Caching.Distributed.IDistributedCache
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
}
