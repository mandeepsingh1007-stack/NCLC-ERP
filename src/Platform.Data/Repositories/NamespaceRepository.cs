using Dapper;
using Npgsql;
using Platform.Core.Auth;

namespace Platform.Data.Repositories;

/// <summary>
/// Dapper implementation of INamespaceRepository.
/// Resolves window/table/column names to DB IDs.
/// </summary>
public class NamespaceRepository : INamespaceRepository
{
    private readonly string _connectionString;

    public NamespaceRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<int?> GetWindowIdAsync(string name)
    {
        var sql = @"SELECT ""SysWindow_ID"" FROM ""SysWindow""
                    WHERE ""Name"" = @Name AND ""IsActive"" = true
                    LIMIT 1";
        using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QueryFirstOrDefaultAsync<int>(sql, new { Name = name });
    }

    public async Task<int?> GetTableIdAsync(string tableName)
    {
        var sql = @"SELECT ""SysTable_ID"" FROM ""SysTable""
                    WHERE ""TableName"" = @TableName AND ""IsActive"" = true
                    LIMIT 1";
        using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QueryFirstOrDefaultAsync<int>(sql, new { TableName = tableName });
    }

    public async Task<int?> GetColumnIdAsync(string tableName, string columnName)
    {
        var sql = @"SELECT c.""SysColumn_ID"" FROM ""SysColumn"" c
                    JOIN ""SysTable"" t ON c.""SysTable_ID"" = t.""SysTable_ID""
                    WHERE t.""TableName"" = @TableName
                      AND c.""ColumnName"" = @ColumnName
                      AND c.""IsActive"" = true
                    LIMIT 1";
        using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QueryFirstOrDefaultAsync<int>(sql, new { TableName = tableName, ColumnName = columnName });
    }
}
