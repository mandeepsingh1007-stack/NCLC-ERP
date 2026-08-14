using Dapper;
using Platform.Core.Metadata;

namespace Platform.Data.Repositories;

public class SysReferenceTableRepository : ISysRepository<SysReferenceTable>
{
    private readonly string _connectionString;

    public SysReferenceTableRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public SysReferenceTable? GetById(int id)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysReference_ID", "SysTable_ID", "KeyColumn", "DisplayColumn",
                   "WhereClause", "OrderByClause"
            FROM SysReferenceTable
            WHERE "SysReference_ID" = @Id
            """;
        return connection.QueryFirstOrDefault<SysReferenceTable>(sql, new { Id = id });
    }

    public IEnumerable<SysReferenceTable> GetAll()
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysReference_ID", "SysTable_ID", "KeyColumn", "DisplayColumn",
                   "WhereClause", "OrderByClause"
            FROM SysReferenceTable
            ORDER BY "SysReference_ID"
            """;
        return connection.Query<SysReferenceTable>(sql);
    }

    public IEnumerable<SysReferenceTable> GetByReferenceId(int sysReferenceId)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysReference_ID", "SysTable_ID", "KeyColumn", "DisplayColumn",
                   "WhereClause", "OrderByClause"
            FROM SysReferenceTable
            WHERE "SysReference_ID" = @SysReferenceId
            """;
        return connection.Query<SysReferenceTable>(sql, new { SysReferenceId = sysReferenceId });
    }

    public int Create(SysReferenceTable entity)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            INSERT INTO SysReferenceTable
                ("SysReference_ID", "SysTable_ID", "KeyColumn", "DisplayColumn", "WhereClause", "OrderByClause")
            VALUES (@SysReferenceId, @SysTableId, @KeyColumn, @DisplayColumn, @WhereClause, @OrderByClause)
            RETURNING "SysReference_ID"
            """;
        return connection.QuerySingle<int>(sql, new
        {
            entity.SysReferenceId,
            entity.SysTableId,
            entity.KeyColumn,
            entity.DisplayColumn,
            entity.WhereClause,
            entity.OrderByClause,
        });
    }

    public void Update(SysReferenceTable entity)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            UPDATE SysReferenceTable
            SET "SysTable_ID" = @SysTableId,
                "KeyColumn" = @KeyColumn,
                "DisplayColumn" = @DisplayColumn,
                "WhereClause" = @WhereClause,
                "OrderByClause" = @OrderByClause
            WHERE "SysReference_ID" = @SysReferenceId
            """;
        connection.Execute(sql, new
        {
            entity.SysReferenceId,
            entity.SysTableId,
            entity.KeyColumn,
            entity.DisplayColumn,
            entity.WhereClause,
            entity.OrderByClause,
        });
    }

    public void Delete(int id)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = "DELETE FROM SysReferenceTable WHERE \"SysReference_ID\" = @Id";
        connection.Execute(sql, new { Id = id });
    }
}
