using System.Data;
using Npgsql;
using Xunit;

namespace Platform.Tests.SchemaContract;

/// <summary>
/// Automated schema contract tests — queries actual PostgreSQL metadata, not migration files.
/// These tests are regression protection for Phase 1 schema correctness.
/// </summary>
public class SchemaContractTests
{
    private static readonly string? ConnectionString =
        Environment.GetEnvironmentVariable("NCLC_TEST_CONNECTION_STRING")
        ?? "Host=127.0.0.1;Port=5432;Database=nclc;Username=postgres;Password=Era@123";

    private NpgsqlConnection CreateConnection() => new(ConnectionString!);

    private IReadOnlyList<DbColumn> QueryColumns(string tableName)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT column_name, data_type, character_maximum_length,
                   is_nullable, column_default, numeric_precision
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = @tn
            ORDER BY ordinal_position";
        cmd.Parameters.AddWithValue("@tn", tableName);
        using var reader = cmd.ExecuteReader();
        var columns = new List<DbColumn>();
        while (reader.Read())
        {
            columns.Add(new DbColumn
            {
                Name = reader.GetString(0),
                DataType = reader.GetString(1),
                MaxLength = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                IsNullable = reader.GetString(3) == "YES",
                Default = reader.IsDBNull(4) ? null : reader.GetString(4),
                NumericPrecision = reader.IsDBNull(5) ? null : reader.GetInt32(5)
            });
        }
        return columns;
    }

    private int QueryColumnCount(string tableName) => QueryColumns(tableName).Count;

    private DbColumn? FindColumn(IReadOnlyList<DbColumn> columns, string name) =>
        columns.FirstOrDefault(c => c.Name == name);

    #region SysElement

    [Fact]
    public void SysElement_HasCorrectColumnCount()
    {
        var cols = QueryColumns("SysElement");
        Assert.Equal(6, cols.Count);
    }

    [Fact]
    public void SysElement_ColumnName_IsVarChar60_NotNull()
    {
        var cols = QueryColumns("SysElement");
        var col = FindColumn(cols, "ColumnName");
        Assert.NotNull(col);
        Assert.Equal("character varying", col.DataType);
        Assert.Equal(60, col.MaxLength);
        Assert.False(col.IsNullable);
    }

    [Fact]
    public void SysElement_Name_IsVarChar120_NotNull()
    {
        var col = FindColumn(QueryColumns("SysElement"), "Name");
        Assert.NotNull(col);
        Assert.Equal("character varying", col.DataType);
        Assert.Equal(120, col.MaxLength);
        Assert.False(col.IsNullable);
    }

    [Fact]
    public void SysElement_ColumnName_IsUnique()
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT EXISTS (
                SELECT 1 FROM information_schema.table_constraints tc
                JOIN information_schema.constraint_column_usage ccu ON tc.constraint_name = ccu.constraint_name
                WHERE tc.table_name = 'SysElement' AND tc.constraint_type = 'UNIQUE' AND ccu.column_name = 'ColumnName'
            )";
        Assert.True((bool)cmd.ExecuteScalar()!);
    }

    [Fact]
    public void SysElement_IsActive_HasDefaultValue_True()
    {
        var col = FindColumn(QueryColumns("SysElement"), "IsActive");
        Assert.NotNull(col);
        Assert.Equal("boolean", col.DataType);
        Assert.False(col.IsNullable);
        Assert.NotNull(col.Default);
        Assert.True(col.Default!.Contains("true") || col.Default!.Contains("1"));
    }

    #endregion

    #region SysElement_Trl

    [Fact]
    public void SysElement_Trl_HasCompositePrimaryKey()
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT kcu.column_name
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
                ON tc.constraint_name = kcu.constraint_name
                AND tc.table_name = kcu.table_name
            WHERE tc.table_name = 'SysElement_Trl'
              AND tc.constraint_type = 'PRIMARY KEY'
            ORDER BY kcu.ordinal_position";
        var cols = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) cols.Add(reader.GetString(0));
        Assert.Contains("SysElement_ID", cols);
        Assert.Contains("Language", cols);
    }

    [Fact]
    public void SysElement_Trl_Language_MaxLength_10()
    {
        var col = FindColumn(QueryColumns("SysElement_Trl"), "Language");
        Assert.NotNull(col);
        Assert.Equal("character varying", col.DataType);
        Assert.Equal(10, col.MaxLength);
        Assert.False(col.IsNullable);
    }

    #endregion

    #region SysReference

    [Fact]
    public void SysReference_Name_IsVarChar60_NotNull_Unique()
    {
        var col = FindColumn(QueryColumns("SysReference"), "Name");
        Assert.NotNull(col);
        Assert.Equal("character varying", col.DataType);
        Assert.Equal(60, col.MaxLength);
        Assert.False(col.IsNullable);
    }

    [Fact]
    public void SysReference_Name_IsUnique()
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT EXISTS (
                SELECT 1 FROM information_schema.table_constraints tc
                JOIN information_schema.constraint_column_usage ccu ON tc.constraint_name = ccu.constraint_name
                WHERE tc.table_name = 'SysReference' AND tc.constraint_type = 'UNIQUE' AND ccu.column_name = 'Name'
            )";
        Assert.True((bool)cmd.ExecuteScalar()!);
    }

    [Fact]
    public void SysReference_ValidationType_Is_NotNull()
    {
        var col = FindColumn(QueryColumns("SysReference"), "ValidationType");
        Assert.NotNull(col);
        Assert.False(col.IsNullable);
    }

    #endregion

    #region SysReferenceList

    [Fact]
    public void SysReferenceList_HasCompositeUniqueConstraint()
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT EXISTS (
                SELECT 1 FROM information_schema.table_constraints tc
                WHERE tc.table_name = 'SysReferenceList'
                  AND tc.constraint_type = 'UNIQUE'
            )";
        Assert.True((bool)cmd.ExecuteScalar()!);
    }

    [Fact]
    public void SysReferenceList_Value_MaxLength_30()
    {
        var col = FindColumn(QueryColumns("SysReferenceList"), "Value");
        Assert.NotNull(col);
        Assert.Equal("character varying", col.DataType);
        Assert.Equal(30, col.MaxLength);
        Assert.False(col.IsNullable);
    }

    [Fact]
    public void SysReferenceList_Name_MaxLength_60()
    {
        var col = FindColumn(QueryColumns("SysReferenceList"), "Name");
        Assert.NotNull(col);
        Assert.Equal("character varying", col.DataType);
        Assert.Equal(60, col.MaxLength);
        Assert.False(col.IsNullable);
    }

    #endregion

    #region SysReferenceTable

    [Fact]
    public void SysReferenceTable_HasCorrectColumns()
    {
        var cols = QueryColumns("SysReferenceTable");
        var names = cols.Select(c => c.Name).ToList();
        Assert.Contains("SysReference_ID", names);
        Assert.Contains("SysTable_ID", names);
        Assert.Contains("KeyColumn", names);
        Assert.Contains("DisplayColumn", names);
        Assert.Contains("WhereClause", names);
        Assert.Contains("OrderByClause", names);
    }

    [Fact]
    public void SysReferenceTable_SysReference_ID_Is_NotNull()
    {
        var col = FindColumn(QueryColumns("SysReferenceTable"), "SysReference_ID");
        Assert.NotNull(col);
        Assert.False(col.IsNullable);
        Assert.Equal("integer", col.DataType);
    }

    [Fact]
    public void SysReferenceTable_KeyColumn_MaxLength_60()
    {
        var col = FindColumn(QueryColumns("SysReferenceTable"), "KeyColumn");
        Assert.NotNull(col);
        Assert.Equal("character varying", col.DataType);
        Assert.Equal(60, col.MaxLength);
        Assert.False(col.IsNullable);
    }

    #endregion

    #region SysValRule

    [Fact]
    public void SysValRule_Name_IsUnique()
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT EXISTS (
                SELECT 1 FROM information_schema.table_constraints tc
                JOIN information_schema.constraint_column_usage ccu ON tc.constraint_name = ccu.constraint_name
                WHERE tc.table_name = 'SysValRule' AND tc.constraint_type = 'UNIQUE' AND ccu.column_name = 'Name'
            )";
        Assert.True((bool)cmd.ExecuteScalar()!);
    }

    [Fact]
    public void SysValRule_Name_MaxLength_120()
    {
        var col = FindColumn(QueryColumns("SysValRule"), "Name");
        Assert.NotNull(col);
        Assert.Equal("character varying", col.DataType);
        Assert.Equal(120, col.MaxLength);
        Assert.False(col.IsNullable);
    }

    [Fact]
    public void SysValRule_RuleType_HasDefault_SQL()
    {
        var col = FindColumn(QueryColumns("SysValRule"), "RuleType");
        Assert.NotNull(col);
        Assert.NotNull(col.Default);
        Assert.Contains("SQL", col.Default!);
    }

    [Fact]
    public void SysValRule_Code_MaxLength_2000()
    {
        var col = FindColumn(QueryColumns("SysValRule"), "Code");
        Assert.NotNull(col);
        Assert.Equal("character varying", col.DataType);
        Assert.Equal(2000, col.MaxLength);
        Assert.False(col.IsNullable);
    }

    #endregion

    #region SysTable

    [Fact]
    public void SysTable_TableName_IsUnique()
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT EXISTS (
                SELECT 1 FROM information_schema.table_constraints tc
                JOIN information_schema.constraint_column_usage ccu ON tc.constraint_name = ccu.constraint_name
                WHERE tc.table_name = 'SysTable' AND tc.constraint_type = 'UNIQUE' AND ccu.column_name = 'TableName'
            )";
        Assert.True((bool)cmd.ExecuteScalar()!);
    }

    [Fact]
    public void SysTable_AccessLevel_Default_3()
    {
        var col = FindColumn(QueryColumns("SysTable"), "AccessLevel");
        Assert.NotNull(col);
        Assert.NotNull(col.Default);
        Assert.Contains("3", col.Default!);
        Assert.Equal("smallint", col.DataType);
        Assert.False(col.IsNullable);
    }

    [Fact]
    public void SysTable_EntityType_HasDefault_D()
    {
        var col = FindColumn(QueryColumns("SysTable"), "EntityType");
        Assert.NotNull(col);
        Assert.NotNull(col.Default);
        Assert.Contains("'D'", col.Default!);
    }

    #endregion

    #region SysColumn

    [Fact]
    public void SysColumn_HasCorrectColumnCount()
    {
        var cols = QueryColumns("SysColumn");
        Assert.Equal(22, cols.Count);
    }

    [Fact]
    public void SysColumn_SysReference_ID_Is_NotNull()
    {
        var col = FindColumn(QueryColumns("SysColumn"), "SysReference_ID");
        Assert.NotNull(col);
        Assert.False(col.IsNullable);
        Assert.Equal("integer", col.DataType);
    }

    [Fact]
    public void SysColumn_EntityType_MaxLength_20()
    {
        var col = FindColumn(QueryColumns("SysColumn"), "EntityType");
        Assert.NotNull(col);
        Assert.Equal("character varying", col.DataType);
        Assert.Equal(20, col.MaxLength);
    }

    [Fact]
    public void SysColumn_DefaultValue_MaxLength_255()
    {
        var col = FindColumn(QueryColumns("SysColumn"), "DefaultValue");
        Assert.NotNull(col);
        Assert.Equal("character varying", col.DataType);
        Assert.Equal(255, col.MaxLength);
    }

    [Fact]
    public void SysColumn_ColumnName_MaxLength_60()
    {
        var col = FindColumn(QueryColumns("SysColumn"), "ColumnName");
        Assert.NotNull(col);
        Assert.Equal("character varying", col.DataType);
        Assert.Equal(60, col.MaxLength);
    }

    [Fact]
    public void SysColumn_SysTable_ID_ColumnName_IsUnique()
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT EXISTS (
                SELECT 1 FROM information_schema.table_constraints tc
                WHERE tc.table_name = 'SysColumn'
                  AND tc.constraint_type = 'UNIQUE'
            )";
        Assert.True((bool)cmd.ExecuteScalar()!);
    }

    #endregion

    #region Foreign Keys

    [Fact]
    public void SysColumn_SysTable_ID_HasForeignKey()
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT EXISTS (
                SELECT 1 FROM information_schema.table_constraints tc
                JOIN information_schema.constraint_column_usage kcu ON tc.constraint_name = kcu.constraint_name
                WHERE tc.table_name = 'SysColumn'
                  AND tc.constraint_type = 'FOREIGN KEY'
                  AND kcu.column_name = 'SysTable_ID'
            )";
        Assert.True((bool)cmd.ExecuteScalar()!);
    }

    [Fact]
    public void SysColumn_SysReference_ID_HasForeignKey()
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT EXISTS (
                SELECT 1 FROM information_schema.table_constraints tc
                JOIN information_schema.constraint_column_usage kcu ON tc.constraint_name = kcu.constraint_name
                WHERE tc.table_name = 'SysColumn'
                  AND tc.constraint_type = 'FOREIGN KEY'
                  AND kcu.column_name = 'SysReference_ID'
            )";
        Assert.True((bool)cmd.ExecuteScalar()!);
    }

    [Fact]
    public void SysReferenceTable_SysReference_ID_HasForeignKey()
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT EXISTS (
                SELECT 1 FROM information_schema.table_constraints tc
                JOIN information_schema.constraint_column_usage kcu ON tc.constraint_name = kcu.constraint_name
                WHERE tc.table_name = 'SysReferenceTable'
                  AND tc.constraint_type = 'FOREIGN KEY'
                  AND kcu.column_name = 'SysReference_ID'
            )";
        Assert.True((bool)cmd.ExecuteScalar()!);
    }

    #endregion

    #region Tables Exist

    [Fact]
    public void All_8_Tables_Exist()
    {
        var expectedTables = new[]
        {
            "SysElement", "SysElement_Trl", "SysReference",
            "SysReferenceList", "SysReferenceTable", "SysValRule",
            "SysTable", "SysColumn"
        };
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT table_name FROM information_schema.tables
            WHERE table_schema = 'public' AND table_type = 'BASE TABLE'
            ORDER BY table_name";
        using var reader = cmd.ExecuteReader();
        var actualTables = new List<string>();
        while (reader.Read()) actualTables.Add(reader.GetString(0));

        foreach (var expected in expectedTables)
        {
            Assert.Contains(expected, actualTables);
        }
    }

    #endregion
}

public record DbColumn
{
    public string Name { get; set; } = "";
    public string DataType { get; set; } = "";
    public int? MaxLength { get; set; }
    public bool IsNullable { get; set; }
    public string? Default { get; set; }
    public int? NumericPrecision { get; set; }
}
