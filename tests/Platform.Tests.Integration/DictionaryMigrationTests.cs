using FluentAssertions;

namespace Platform.Tests.Integration;

/// <summary>
/// Integration tests for Dictionary Foundation migrations.
/// In CI, migrations are run via psql before tests execute.
/// In local dev (no connection string), migration SQL is executed inline.
/// </summary>
public class DictionaryMigrationTests : IAsyncLifetime
{
    private Npgsql.NpgsqlConnection? _connection;
    private readonly bool _migrationsPreApplied;

    public DictionaryMigrationTests()
    {
        // If connection string is provided, we assume migrations were already run (CI mode).
        // Otherwise, we create an inline container and apply migrations ourselves (local dev).
        var envConnStr = Environment.GetEnvironmentVariable("NCLC_TEST_CONNECTION_STRING");
        _migrationsPreApplied = !string.IsNullOrEmpty(envConnStr);
    }

    public async Task InitializeAsync()
    {
        if (_migrationsPreApplied)
        {
            _connection = new Npgsql.NpgsqlConnection(
                Environment.GetEnvironmentVariable("NCLC_TEST_CONNECTION_STRING")!);
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

            // Apply migrations in local dev mode
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
    public async Task All_8_Dictionary_Tables_Should_Exist()
    {
        // Verify all 8 tables exist
        var tables = new[]
        {
            "SysElement", "SysElement_Trl", "SysReference", "SysReferenceList",
            "SysValRule", "SysTable", "SysReferenceTable", "SysColumn"
        };

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
    public async Task SysColumn_ShouldHave_All_Required_Columns()
    {
        using var checkCmd = new Npgsql.NpgsqlCommand("""
            SELECT column_name
            FROM information_schema.columns
            WHERE table_name = 'SysColumn'
            ORDER BY ordinal_position
            """, _connection!);
        using var reader = await checkCmd.ExecuteReaderAsync();
        var columns = new HashSet<string>();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        var expectedColumns = new[]
        {
            "SysColumn_ID", "SysTable_ID", "ColumnName", "SysElement_ID",
            "SysReference_ID", "SysReferenceValue_ID", "SysValRule_ID",
            "FieldLength", "IsMandatory", "IsKey", "SeqNo",
            "EntityType", "IsActive"
        };

        foreach (var col in expectedColumns)
        {
            columns.Should().Contain(col, $"SysColumn should have column '{col}'");
        }
    }

    [Fact]
    public async Task SysTable_ShouldHave_All_Required_Columns()
    {
        using var checkCmd = new Npgsql.NpgsqlCommand("""
            SELECT column_name
            FROM information_schema.columns
            WHERE table_name = 'SysTable'
            ORDER BY ordinal_position
            """, _connection!);
        using var reader = await checkCmd.ExecuteReaderAsync();
        var columns = new HashSet<string>();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        columns.Should().Contain(new[]
        {
            "SysTable_ID", "TableName", "ClassName", "AccessLevel", "EntityType"
        });
    }

    [Fact]
    public async Task SysReference_Should_Be_Seeded()
    {
        using var cmd = new Npgsql.NpgsqlCommand("SELECT COUNT(*) FROM \"SysReference\"", _connection!);
        var count = (int)((await cmd.ExecuteScalarAsync())!);
        count.Should().BeGreaterThanOrEqualTo(11);
    }

    [Fact]
    public async Task SysValRule_Should_Be_Seeded()
    {
        using var cmd = new Npgsql.NpgsqlCommand("SELECT COUNT(*) FROM \"SysValRule\"", _connection!);
        var count = (int)((await cmd.ExecuteScalarAsync())!);
        count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task SysTable_Should_Be_Seeded()
    {
        using var cmd = new Npgsql.NpgsqlCommand("SELECT COUNT(*) FROM \"SysTable\"", _connection!);
        var count = (int)((await cmd.ExecuteScalarAsync())!);
        count.Should().BeGreaterThanOrEqualTo(7);
    }

    [Fact]
    public async Task SysElement_Should_Be_Seeded()
    {
        using var cmd = new Npgsql.NpgsqlCommand("SELECT COUNT(*) FROM \"SysElement\"", _connection!);
        var count = (int)((await cmd.ExecuteScalarAsync())!);
        count.Should().BeGreaterThanOrEqualTo(27);
    }

    [Fact]
    public async Task SysReferenceTable_Should_Be_Seeded()
    {
        using var cmd = new Npgsql.NpgsqlCommand("SELECT COUNT(*) FROM \"SysReferenceTable\"", _connection!);
        var count = (int)((await cmd.ExecuteScalarAsync())!);
        count.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task ForeignKeys_Should_Have_No_Orphans()
    {
        // SysReferenceList -> SysReference
        using var cmd1 = new Npgsql.NpgsqlCommand("""
            SELECT COUNT(*) FROM \"SysReferenceList\" r
            LEFT JOIN \"SysReference\" s ON r.\"SysReference_ID\" = s.\"SysReference_ID\"
            WHERE s.\"SysReference_ID\" IS NULL
            """, _connection!);
        var orphans1 = (int)(await cmd1.ExecuteScalarAsync()!);
        orphans1.Should().Be(0);

        // SysReferenceTable -> SysReference
        using var cmd2 = new Npgsql.NpgsqlCommand("""
            SELECT COUNT(*) FROM \"SysReferenceTable\" r
            LEFT JOIN \"SysReference\" s ON r.\"SysReference_ID\" = s.\"SysReference_ID\"
            WHERE s.\"SysReference_ID\" IS NULL
            """, _connection!);
        var orphans2 = (int)(await cmd2.ExecuteScalarAsync()!);
        orphans2.Should().Be(0);

        // SysColumn -> SysTable
        using var cmd3 = new Npgsql.NpgsqlCommand("""
            SELECT COUNT(*) FROM \"SysColumn\" c
            LEFT JOIN \"SysTable\" t ON c.\"SysTable_ID\" = t.\"SysTable_ID\"
            WHERE t.\"SysTable_ID\" IS NULL
            """, _connection!);
        var orphans3 = (int)(await cmd3.ExecuteScalarAsync()!);
        orphans3.Should().Be(0);
    }

    [Fact]
    public async Task UNIQUE_Constraints_Should_Be_Enforced()
    {
        // Try to insert duplicate SysReference.Name
        using var insert = new Npgsql.NpgsqlCommand(
            "INSERT INTO \"SysReference\" (\"Name\", \"ValidationType\") VALUES ('DupTest', 'LIST')",
            _connection!);
        await insert.ExecuteNonQueryAsync();

        using var dup = new Npgsql.NpgsqlCommand(
            "INSERT INTO \"SysReference\" (\"Name\", \"ValidationType\") VALUES ('DupTest', 'LIST')",
            _connection!);
        var ex = await Assert.ThrowsAsync<Npgsql.PostgresException>(
            () => dup.ExecuteNonQueryAsync());
        ex.SqlState.Should().Be("23505");

        // Clean up
        using var del = new Npgsql.NpgsqlCommand(
            "DELETE FROM \"SysReference\" WHERE \"Name\" = 'DupTest'", _connection!);
        await del.ExecuteNonQueryAsync();
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
        throw new InvalidOperationException(
            "Could not find repository root from: " + AppContext.BaseDirectory);
    }
}
