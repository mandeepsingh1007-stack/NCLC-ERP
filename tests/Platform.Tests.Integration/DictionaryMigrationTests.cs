using FluentAssertions;
using Platform.Core.Metadata;
using Platform.Data.Repositories;
using Testcontainers.PostgreSql;

namespace Platform.Tests.Integration;

public class DictionaryMigrationTests : IAsyncLifetime
{
    private readonly Npgsql.NpgsqlConnection _connection;
    private readonly PostgreSqlContainer _container;
    private readonly string _connectionString;

    public DictionaryMigrationTests()
    {
        _container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithPassword("testpass")
            .Build();

        _connectionString = _container.GetConnectionString();
        _connection = new Npgsql.NpgsqlConnection(_connectionString);
    }

    public Task InitializeAsync()
    {
        return _connection.OpenAsync();
    }

    public Task DisposeAsync()
    {
        _connection.Close();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Migration_001_ShouldCreateAllDictionaryTables()
    {
        // Read and execute migration SQL
        var migrationPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../src/Platform.Data/Migrations/001_Create_Dictionary_Schema.sql"));
        var sql = await File.ReadAllTextAsync(migrationPath);

        using var cmd = new Npgsql.NpgsqlCommand(sql, _connection);
        await cmd.ExecuteNonQueryAsync();

        // Verify tables exist
        var tables = new[] { "SysElement", "SysElement_Trl", "SysReference", "SysReferenceList",
            "SysValRule", "SysTable", "SysColumn" };

        foreach (var table in tables)
        {
            using var checkCmd = new Npgsql.NpgsqlCommand(
                $"SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = '{table}')",
                _connection);
            var exists = (bool)await checkCmd.ExecuteScalarAsync();
            exists.Should().BeTrue($"Table {table} should exist after migration 001");
        }
    }

    [Fact]
    public async Task Migration_002_ShouldSeedReferenceTypes()
    {
        // Run both migrations
        var sql1 = await File.ReadAllTextAsync(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../src/Platform.Data/Migrations/001_Create_Dictionary_Schema.sql")));
        var sql2 = await File.ReadAllTextAsync(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../src/Platform.Data/Migrations/002_Seed_Dictionary_Data.sql")));

        using var cmd1 = new Npgsql.NpgsqlCommand(sql1, _connection);
        await cmd1.ExecuteNonQueryAsync();

        using var cmd2 = new Npgsql.NpgsqlCommand(sql2, _connection);
        await cmd2.ExecuteNonQueryAsync();

        // Verify seed data
        using var checkCmd = new Npgsql.NpgsqlCommand(
            "SELECT COUNT(*) FROM \"SysReference\"", _connection);
        var count = (int)await checkCmd.ExecuteScalarAsync();
        count.Should().BeGreaterThanOrEqualTo(11, "At least 11 reference types should be seeded");
    }
}
