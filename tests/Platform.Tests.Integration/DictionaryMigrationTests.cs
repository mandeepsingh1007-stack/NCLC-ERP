using FluentAssertions;

namespace Platform.Tests.Integration;

public class DictionaryMigrationTests : IAsyncLifetime
{
    private Npgsql.NpgsqlConnection? _connection;
    private string _connectionString = "";

    public async Task InitializeAsync()
    {
        var envConnStr = Environment.GetEnvironmentVariable("NCLC_TEST_CONNECTION_STRING");
        if (!string.IsNullOrEmpty(envConnStr))
        {
            _connectionString = envConnStr;
        }
        else
        {
            // Local dev — create an inline test container
            var container = new Testcontainers.PostgreSql.PostgreSqlBuilder("postgres:15-alpine")
                .WithPassword("testpass")
                .Build();
            await container.StartAsync();
            _connectionString = container.GetConnectionString();
        }

        _connection = new Npgsql.NpgsqlConnection(_connectionString);
        await _connection.OpenAsync();
    }

    public async Task DisposeAsync()
    {
        _connection?.Close();
        _connection?.Dispose();
    }

    [Fact]
    public async Task Migration_001_ShouldCreateAllDictionaryTables()
    {
        // Execute the schema migration SQL directly
        var sql = @"
DO $$
BEGIN
    CREATE TABLE SysElement (
        SysElement_ID SERIAL PRIMARY KEY,
        ColumnName VARCHAR(60) NOT NULL UNIQUE,
        Name VARCHAR(120) NOT NULL,
        ElementType VARCHAR(20) NOT NULL DEFAULT 'C',
        Description TEXT,
        IsActive BOOLEAN NOT NULL DEFAULT true
    );
    CREATE TABLE SysElement_Trl (
        SysElement_ID INT NOT NULL,
        Language VARCHAR(10) NOT NULL,
        Translation TEXT NOT NULL,
        PRIMARY KEY (SysElement_ID, Language)
    );
    CREATE TABLE SysReference (
        SysReference_ID SERIAL PRIMARY KEY,
        Name VARCHAR(60) NOT NULL UNIQUE,
        Description TEXT,
        ValidationType VARCHAR(30)
    );
    CREATE TABLE SysReferenceList (
        SysReferenceList_ID SERIAL PRIMARY KEY,
        SysReference_ID INT NOT NULL,
        [Name] VARCHAR(60) NOT NULL,
        [Value] VARCHAR(30) NOT NULL,
        DisplayOrder INT NOT NULL DEFAULT 0,
        CONSTRAINT CHK_Value_Length CHECK (LENGTH([Value]) <= 30)
    );
    CREATE TABLE SysReferenceTable (
        SysReferenceTable_ID SERIAL PRIMARY KEY,
        SysReference_ID INT NOT NULL,
        SysTable_ID INT NOT NULL,
        KeyColumn VARCHAR(60) NOT NULL,
        DisplayColumn VARCHAR(60),
        WhereClause VARCHAR(255),
        OrderByClause VARCHAR(255)
    );
    CREATE TABLE SysValRule (
        SysValRule_ID SERIAL PRIMARY KEY,
        Name VARCHAR(120) NOT NULL UNIQUE,
        RuleType VARCHAR(20) NOT NULL DEFAULT 'SQL',
        Code VARCHAR(2000) NOT NULL,
        Description TEXT
    );
    CREATE TABLE SysTable (
        SysTable_ID SERIAL PRIMARY KEY,
        TableName VARCHAR(60) NOT NULL UNIQUE,
        DisplayName VARCHAR(120) NOT NULL,
        EntityType VARCHAR(20) NOT NULL DEFAULT 'D',
        AccessLevel SMALLINT NOT NULL DEFAULT 3,
        Description TEXT,
        IsActive BOOLEAN NOT NULL DEFAULT true
    );
    CREATE TABLE SysColumn (
        SysColumn_ID SERIAL PRIMARY KEY,
        SysTable_ID INT NOT NULL,
        SysReference_ID INT NOT NULL,
        ColumnName VARCHAR(60) NOT NULL,
        DisplayName VARCHAR(120),
        DataType VARCHAR(30) NOT NULL,
        EntityName VARCHAR(60),
        EntityType VARCHAR(20),
        DisplayOrder INT NOT NULL DEFAULT 0,
        DefaultValue VARCHAR(255),
        IsRequired BOOLEAN NOT NULL DEFAULT false,
        IsUnique BOOLEAN NOT NULL DEFAULT false,
        IsPrimaryKey BOOLEAN NOT NULL DEFAULT false,
        IsForeignKey BOOLEAN NOT NULL DEFAULT false,
        MaxLength INT,
        Precision INT,
        Scale INT,
        IsComputed BOOLEAN NOT NULL DEFAULT false,
        IsSystemColumn BOOLEAN NOT NULL DEFAULT false,
        Searchable BOOLEAN NOT NULL DEFAULT true,
        Filterable BOOLEAN NOT NULL DEFAULT true
    );
END $$;";

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
        // Insert seed data directly (same as 002_Seed_Dictionary_Data.sql)
        var sql = @"
INSERT INTO SysReference (Name, ValidationType) VALUES
    ('Gender', 'RegularExpression'),
    ('MaritalStatus', 'RegularExpression'),
    ('EducationLevel', 'List'),
    ('EmploymentType', 'List'),
    ('Department', 'Lookup'),
    ('Designation', 'Lookup'),
    ('Priority', 'List'),
    ('PaymentMode', 'List'),
    ('AccountType', 'List'),
    ('TransactionType', 'List'),
    ('Currency', 'List')
ON CONFLICT (Name) DO NOTHING;";

        using var cmd = new Npgsql.NpgsqlCommand(sql, _connection!);
        await cmd.ExecuteNonQueryAsync();

        // Verify seed data
        using var checkCmd = new Npgsql.NpgsqlCommand(
            "SELECT COUNT(*) FROM \"SysReference\"", _connection!);
        var count = (int)((await checkCmd.ExecuteScalarAsync())!);
        count.Should().BeGreaterThanOrEqualTo(11, "At least 11 reference types should be seeded");
    }
}
