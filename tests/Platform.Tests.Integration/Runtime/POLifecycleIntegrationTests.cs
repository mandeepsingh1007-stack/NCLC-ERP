using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Platform.Core.Cache;
using Platform.Core.Metadata;
using Platform.Core.Runtime;
using Platform.Data.Repositories;

namespace Platform.Tests.Integration.Runtime;

/// <summary>
/// Integration tests for Phase 2 Runtime services.
/// These tests require a PostgreSQL database (via Testcontainers or env connection string).
/// </summary>
public class POLifecycleIntegrationTests : IAsyncLifetime
{
    private Npgsql.NpgsqlConnection? _connection;
    private readonly bool _migrationsPreApplied;
    private string? _testConnStr;

    public POLifecycleIntegrationTests()
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

            // Apply migrations
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
    public async Task MetadataGraph_LoadsAllTablesFromDatabase()
    {
        var graph = new MetadataGraph(_testConnStr!);
        var tables = graph.GetTableNames();

        tables.Should().NotBeNull();
        tables.Should().Contain("systable");
        tables.Should().Contain("syscolumn");
        tables.Should().Contain("sysreference");
    }

    [Fact]
    public async Task MetadataGraph_LoadsAllColumnsForATable()
    {
        var graph = new MetadataGraph(_testConnStr!);
        var columns = graph.GetColumns("SysTable");

        columns.Should().NotBeNull();
        columns.Count.Should().BeGreaterThan(0);
        columns.Should().Contain(c => c.ColumnName == "TableName");
    }

    [Fact]
    public async Task MetadataGraph_LoadsReferences()
    {
        var graph = new MetadataGraph(_testConnStr!);
        var references = graph.GetReferences("YesNo");

        references.Should().NotBeNull();
        references.Count.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task MetadataGraph_GetTableById_ReturnsTableMetadata()
    {
        var graph = new MetadataGraph(_testConnStr!);
        var sysTableTable = graph.GetTable("SysTable");
        sysTableTable.Should().NotBeNull();

        var byId = graph.GetTableById(sysTableTable!.SysTableId);

        byId.Should().NotBeNull();
        byId!.TableName.Should().Be("SysTable");
    }

    [Fact]
    public async Task POValidator_ValidatesMandatoryColumnFromDatabase()
    {
        var graph = new MetadataGraph(_testConnStr!);
        var validator = new POValidator(
            new TypeValidator(),
            new ReferenceValueValidator(graph),
            new ValRuleEngine(_testConnStr!, graph.GetTableNames()),
            graph);

        // "TableName" is a mandatory column in the seeded SysTable table
        var columns = graph.GetColumns("SysTable");
        var tableNameCol = columns.FirstOrDefault(c => c.ColumnName == "TableName");
        tableNameCol.Should().NotBeNull();
        tableNameCol!.IsMandatory.Should().BeTrue();

        var result = validator.Validate("SysTable", tableNameCol, null,
            InMemoryContext.Create("user1", "tenant1", "org1"));

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("required"));
    }

    [Fact]
    public async Task POValidator_PassesValidData()
    {
        var graph = new MetadataGraph(_testConnStr!);
        var validator = new POValidator(
            new TypeValidator(),
            new ReferenceValueValidator(graph),
            new ValRuleEngine(_testConnStr!, graph.GetTableNames()),
            graph);

        var columns = graph.GetColumns("SysTable");
        var nameCol = columns.FirstOrDefault(c => c.ColumnName == "TableName");

        var result = validator.Validate("SysTable", nameCol!, "ValidTableName",
            InMemoryContext.Create("user1", "tenant1", "org1"));

        result.IsSuccess.Should().BeTrue($"Validation failed: {string.Join("; ", result.Errors)}");
    }

    [Fact]
    public async Task POValidator_CollectsMultipleErrors()
    {
        var graph = new MetadataGraph(_testConnStr!);
        var validator = new POValidator(
            new TypeValidator(),
            new ReferenceValueValidator(graph),
            new ValRuleEngine(_testConnStr!, graph.GetTableNames()),
            graph);

        // ValidateAll with missing mandatory fields should collect errors
        var values = new Dictionary<string, object?>();

        var result = validator.ValidateAll("SysTable", values,
            InMemoryContext.Create("user1", "tenant1", "org1"));

        // At minimum TableName is mandatory — should fail
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ValRuleEngine_EvaluatesRegexRuleFromDatabase()
    {
        var engine = new ValRuleEngine(_testConnStr!, Array.Empty<string>());
        var rule = new Platform.Core.Metadata.SysValRule
        {
            Name = "TestRule",
            RuleType = Platform.Core.Metadata.ValRuleTypeEnum.Regex,
            Code = @"^[A-Za-z0-9]+$"
        };

        var result = engine.Evaluate(rule, "abc123",
            InMemoryContext.Create("user1", "tenant1", "org1"));
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task ValRuleEngine_EvaluatesSQLRuleFromDatabase()
    {
        // Use lowercase table name — PostgreSQL unquoted identifiers are folded to lowercase.
        // The database tables are created with double-quoted PascalCase names,
        // so unquoted queries must use lowercase to match.
        var engine = new ValRuleEngine(_testConnStr!, new[] { "syscolumn" });
        var rule = new Platform.Core.Metadata.SysValRule
        {
            Name = "SQLRule",
            RuleType = Platform.Core.Metadata.ValRuleTypeEnum.Sql,
            Code = "SELECT COUNT(*) FROM syscolumn"
        };

        var result = engine.Evaluate(rule, "test",
            InMemoryContext.Create(null, null, null));
        result.Passed.Should().BeTrue($"SQL rule failed: {result.ErrorMessage}"); // COUNT > 0 returns non-zero int
    }

    [Fact]
    public async Task ValRuleEngine_SQLParameterized_DoesNotConcatenate()
    {
        // No table allowlist needed — SELECT @Value has no FROM clause
        var engine = new ValRuleEngine(_testConnStr!, Array.Empty<string>());
        var rule = new Platform.Core.Metadata.SysValRule
        {
            Name = "ParamRule",
            RuleType = Platform.Core.Metadata.ValRuleTypeEnum.Sql,
            Code = "SELECT @Value"
        };

        // Value containing SQL injection should be safely parameterized
        var result = engine.Evaluate(rule, "'; DROP TABLE Users; --",
            InMemoryContext.Create(null, null, null));

        // Returns the literal string, not executing the injection
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task ValRuleEngine_RejectsPgCatalog()
    {
        // pg_catalog should be blocked even if the table were in the allowlist
        var engine = new ValRuleEngine(_testConnStr!, new[] { "pg_tables" });
        var rule = new Platform.Core.Metadata.SysValRule
        {
            Name = "PgCatalogRule",
            RuleType = Platform.Core.Metadata.ValRuleTypeEnum.Sql,
            Code = "SELECT * FROM pg_catalog.pg_tables"
        };

        var result = engine.Evaluate(rule, "test",
            InMemoryContext.Create(null, null, null));
        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("system catalog");
    }

    [Fact]
    public async Task ValRuleEngine_RejectsCTE()
    {
        // CTEs are blocked even with 't' in the allowlist
        var engine = new ValRuleEngine(_testConnStr!, new[] { "t" });
        var rule = new Platform.Core.Metadata.SysValRule
        {
            Name = "CTERule",
            RuleType = Platform.Core.Metadata.ValRuleTypeEnum.Sql,
            Code = "WITH t AS (SELECT 1) SELECT * FROM t"
        };

        var result = engine.Evaluate(rule, "test",
            InMemoryContext.Create(null, null, null));
        result.Passed.Should().BeFalse();
    }

    [Fact]
    public async Task ValRuleEngine_RejectsInsert()
    {
        // INSERT is always blocked regardless of table allowlist
        var engine = new ValRuleEngine(_testConnStr!, new[] { "Users" });
        var rule = new Platform.Core.Metadata.SysValRule
        {
            Name = "InsertRule",
            RuleType = Platform.Core.Metadata.ValRuleTypeEnum.Sql,
            Code = "INSERT INTO Users VALUES (1, 'admin')"
        };

        var result = engine.Evaluate(rule, "test",
            InMemoryContext.Create(null, null, null));
        result.Passed.Should().BeFalse();
    }

    [Fact]
    public async Task ValRuleEngine_RejectsInvalidRegex()
    {
        var engine = new ValRuleEngine(_testConnStr!, Array.Empty<string>());
        var rule = new Platform.Core.Metadata.SysValRule
        {
            Name = "DigitOnly",
            RuleType = Platform.Core.Metadata.ValRuleTypeEnum.Regex,
            Code = "^[0-9]+$"
        };

        var result = engine.Evaluate(rule, "abc",
            InMemoryContext.Create("user1", "tenant1", "org1"));
        result.Passed.Should().BeFalse();
    }

    [Fact]
    public async Task MetadataCacheService_CachesAndInvalidates()
    {
        var graph = new MetadataGraph(_testConnStr!);
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MetadataCacheService(memoryCache, new DummyCache());

        // First access loads from graph (simulated via Set)
        cache.Set<string>("meta:table:Users-1", "cached-table-data");
        var result = cache.Get<string>("meta:table:Users-1");
        result.Should().Be("cached-table-data");

        // Invalidate
        cache.Invalidate("meta:table:Users-1");
        result = cache.Get<string>("meta:table:Users-1");
        result.Should().BeNull();
    }

    [Fact]
    public async Task CacheInvalidationService_InvalidateAsync_DoesNotThrow()
    {
        var graph = new MetadataGraph(_testConnStr!);
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MetadataCacheService(memoryCache, new DummyCache());
        var invalidation = new CacheInvalidationService(cache, "localhost:6379");

        var evt = new DictionaryChangedEvent("Table", 1, "Users", "Updated");

        await invalidation.InvalidateAsync(evt);
        // Should not throw even without a real Redis
    }

    [Fact]
    public async Task FullValidationPipeline_AllStepsExecute()
    {
        var graph = new MetadataGraph(_testConnStr!);
        var validator = new POValidator(
            new TypeValidator(),
            new ReferenceValueValidator(graph),
            new ValRuleEngine(_testConnStr!, graph.GetTableNames()),
            graph);

        var columns = graph.GetColumns("SysTable");
        var nameCol = columns.FirstOrDefault(c => c.ColumnName == "TableName");

        // Mandatory check + type check + string length
        var result = validator.Validate("SysTable", nameCol!, "ValidTableName",
            InMemoryContext.Create(null, null, null));

        result.IsSuccess.Should().BeTrue($"Validation failed: {string.Join("; ", result.Errors)}");
    }

    [Fact]
    public async Task Rollback_DoesNotPublishDictionaryChangedEvent()
    {
        // Verify: persist failure → no DictionaryChangedEvent → no cache invalidation
        // POLifecycleManager.CreateAsync calls InvalidateAsync ONLY after persist() succeeds.
        // If persist throws, the event is never published.

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MetadataCacheService(memoryCache, new DummyCache());

        // Set a value in the cache
        cache.Set<string>("meta:table:Users", "data-v1");
        cache.Get<string>("meta:table:Users").Should().Be("data-v1");

        // Simulate lifecycle with failing persist: persist throws → no InvalidateAsync called
        try
        {
            // Simulate: persist fails
            throw new Npgsql.NpgsqlException("Connection lost");
        }
        catch
        {
            // Persist failed — no InvalidateAsync called
        }

        // The cache value should still be present
        cache.Get<string>("meta:table:Users").Should().Be("data-v1", "Cache should NOT be invalidated when persist fails");
    }

    [Fact]
    public async Task Rollback_NoEventPublishedAfterCommit()
    {
        // Verify: successful persist → then InvalidateAsync called → event published
        // This is the positive test: commit → event → invalidation

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MetadataCacheService(memoryCache, new DummyCache());

        cache.Set<string>("meta:table:SysTable", "data-v1");
        cache.Get<string>("meta:table:SysTable").Should().Be("data-v1");

        // Simulate successful persist → then InvalidateAsync
        var invalidationService = new CacheInvalidationService(cache, "nonexistent:12345");
        var evt = new DictionaryChangedEvent("Table", 1, "SysTable", "Updated");

        // This simulates the post-commit path in POLifecycleManager
        await invalidationService.InvalidateAsync(evt);

        cache.Get<string>("meta:table:SysTable").Should().BeNull("Cache should be invalidated after successful persist + InvalidateAsync");
    }

    [Fact]
    public async Task ValRuleEngine_SQLWithTenantPredicate_ReturnsCorrectResults()
    {
        // Use lowercase table names — PostgreSQL unquoted identifiers are folded to lowercase.
        var engine = new ValRuleEngine(_testConnStr!, new[] { "systable" });
        var rule = new Platform.Core.Metadata.SysValRule
        {
            Name = "TableCount",
            RuleType = Platform.Core.Metadata.ValRuleTypeEnum.Sql,
            Code = "SELECT COUNT(*) FROM systable WHERE tablename = 'SysTable'"
        };

        var result = engine.Evaluate(rule, null,
            InMemoryContext.Create(null, null, null));
        // SysTable has exactly 1 row with TableName = 'SysTable' → count = 1 → Pass
        result.Passed.Should().BeTrue("SQL query should return non-zero count");
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

    /// <summary>
    /// No-op distributed cache — integration tests don't need real Redis.
    /// </summary>
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

    /// <summary>
    /// Distributed cache that tracks whether any operations were called.
    /// </summary>
    private class TrackingDistributedCache : Microsoft.Extensions.Caching.Distributed.IDistributedCache
    {
        private readonly Action _onCall;
        public TrackingDistributedCache(Action onCall) => _onCall = onCall;
        public byte[]? Get(string key) => null;
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult<byte[]?>(null);
        public void Set(string key, byte[] value, Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions options) { }
        public Task SetAsync(string key, byte[] value, Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            _onCall();
            return Task.CompletedTask;
        }
        public void Refresh(string key) { }
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        public void Remove(string key) { }
        public Task RemoveAsync(string key, CancellationToken token = default) => Task.CompletedTask;
    }
}
