using FluentAssertions;
using Platform.Core.Metadata;
using Platform.Data.Repositories;
using Testcontainers.PostgreSql;

namespace Platform.Tests.Integration;

public class DictionaryMigrationTests : IAsyncLifetime
{
    private Npgsql.NpgsqlConnection? _connection;
    private PostgreSqlContainer? _container;
    private string _connectionString = "";

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:15-alpine")
            .WithPassword("testpass")
            .Build();
        await _container.StartAsync();

        _connectionString = _container.GetConnectionString();
        _connection = new Npgsql.NpgsqlConnection(_connectionString);
        await _connection.OpenAsync();
    }

    public async Task DisposeAsync()
    {
        _connection?.Close();
        _connection?.Dispose();
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    private string GetMigrationPath(string fileName)
    {
        // Try multiple possible base directories for finding migration files
        var possiblePaths = new[]
        {
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory(),
            Environment.CurrentDirectory,
            Path.GetDirectoryName(typeof(DictionaryMigrationTests).Assembly.Location)!
        };

        var relativeParts = new[] { "..", "..", "..", "..", "src", "Platform.Data", "Migrations" };

        foreach (var baseDir in possiblePaths)
        {
            var candidate = Path.Combine(baseDir, "..", "..", "..", "..", "src", "Platform.Data", "Migrations", fileName);
            var resolved = Path.GetFullPath(candidate);
            if (File.Exists(resolved))
                return resolved;
        }

        // Fallback: look from repo root
        var repoRoot = FindRepoRoot();
        return Path.Combine(repoRoot, "src", "Platform.Data", "Migrations", fileName);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (Directory.GetFiles(dir, "*.slnx").Length > 0 || Directory.GetFiles(dir, "*.sln").Length > 0)
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        return Directory.GetCurrentDirectory();
    }

    [Fact]
    public async Task Migration_001_ShouldCreateAllDictionaryTables()
    {
        var migrationPath = GetMigrationPath("001_Create_Dictionary_Schema.sql");
        var sql = await File.ReadAllTextAsync(migrationPath);

        using var cmd = new Npgsql.NpgsqlCommand(sql, _connection!);
        await cmd.ExecuteNonQueryAsync();

        // Verify tables exist
        var tables = new[] { "SysElement", "SysElement_Trl", "SysReference", "SysReferenceList",
            "SysReferenceTable", "SysValRule", "SysTable", "SysColumn" };

        foreach (var table in tables)
        {
            using var checkCmd = new Npgsql.NpgsqlCommand(
                $"SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = '{table}')",
                _connection!);
            var exists = (bool)((await checkCmd.ExecuteScalarAsync())!);
            exists.Should().BeTrue($"Table {table} should exist after migration 001");
        }
    }

    [Fact]
    public async Task Migration_002_ShouldSeedReferenceTypes()
    {
        var sql1 = await File.ReadAllTextAsync(GetMigrationPath("001_Create_Dictionary_Schema.sql"));
        var sql2 = await File.ReadAllTextAsync(GetMigrationPath("002_Seed_Dictionary_Data.sql"));

        using var cmd1 = new Npgsql.NpgsqlCommand(sql1, _connection!);
        await cmd1.ExecuteNonQueryAsync();

        using var cmd2 = new Npgsql.NpgsqlCommand(sql2, _connection!);
        await cmd2.ExecuteNonQueryAsync();

        // Verify seed data
        using var checkCmd = new Npgsql.NpgsqlCommand(
            "SELECT COUNT(*) FROM \"SysReference\"", _connection!);
        var count = (int)((await checkCmd.ExecuteScalarAsync())!);
        count.Should().BeGreaterThanOrEqualTo(11, "At least 11 reference types should be seeded");
    }
}
