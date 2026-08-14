using Dapper;
using Platform.Core.Metadata;

namespace Platform.Data.Repositories;

public class SysReferenceListRepository : ISysRepository<SysReferenceList>
{
    private readonly string _connectionString;

    public SysReferenceListRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public SysReferenceList? GetById(int id)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysReferenceList_ID", "SysReference_ID", "Value", "Name", "SeqNo", "IsActive"
            FROM "SysReferenceList"
            WHERE "SysReferenceList_ID" = @Id
            """;
        return connection.QueryFirstOrDefault<SysReferenceList>(sql, new { Id = id });
    }

    public IEnumerable<SysReferenceList> GetAll()
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysReferenceList_ID", "SysReference_ID", "Value", "Name", "SeqNo", "IsActive"
            FROM "SysReferenceList"
            ORDER BY "SeqNo"
            """;
        return connection.Query<SysReferenceList>(sql);
    }

    public IEnumerable<SysReferenceList> GetByReferenceId(int sysReferenceId)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysReferenceList_ID", "SysReference_ID", "Value", "Name", "SeqNo", "IsActive"
            FROM "SysReferenceList"
            WHERE "SysReference_ID" = @SysReferenceId
              AND "IsActive" = TRUE
            ORDER BY "SeqNo"
            """;
        return connection.Query<SysReferenceList>(sql, new { SysReferenceId = sysReferenceId });
    }

    public int Create(SysReferenceList entity)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            INSERT INTO "SysReferenceList" ("SysReference_ID", "Value", "Name", "SeqNo", "IsActive")
            VALUES (@SysReferenceId, @Value, @Name, @SeqNo, @IsActive)
            RETURNING "SysReferenceList_ID"
            """;
        return connection.QuerySingle<int>(sql, new
        {
            entity.SysReferenceId,
            entity.Value,
            entity.Name,
            entity.SeqNo,
            entity.IsActive,
        });
    }

    public void Update(SysReferenceList entity)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            UPDATE "SysReferenceList"
            SET "Value" = @Value,
                "Name" = @Name,
                "SeqNo" = @SeqNo,
                "IsActive" = @IsActive
            WHERE "SysReferenceList_ID" = @SysReferenceListId
            """;
        connection.Execute(sql, new
        {
            entity.SysReferenceListId,
            entity.Value,
            entity.Name,
            entity.SeqNo,
            entity.IsActive,
        });
    }

    public void Delete(int id)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            DELETE FROM "SysReferenceList" WHERE "SysReferenceList_ID" = @Id
            """;
        connection.Execute(sql, new { Id = id });
    }
}
