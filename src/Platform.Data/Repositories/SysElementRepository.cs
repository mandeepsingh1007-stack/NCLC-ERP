using Dapper;
using Platform.Core.Metadata;

namespace Platform.Data.Repositories;

public class SysElementRepository : ISysRepository<SysElement>
{
    private readonly string _connectionString;

    public SysElementRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public SysElement? GetById(int id)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysElement_ID", "ColumnName", "Name", "Description", "Help", "IsActive"
            FROM "SysElement"
            WHERE "SysElement_ID" = @Id
            """;
        return connection.QueryFirstOrDefault<SysElement>(sql, new { Id = id });
    }

    public IEnumerable<SysElement> GetAll()
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysElement_ID", "ColumnName", "Name", "Description", "Help", "IsActive"
            FROM "SysElement"
            ORDER BY "SysElement_ID"
            """;
        return connection.Query<SysElement>(sql);
    }

    public int Create(SysElement entity)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            INSERT INTO "SysElement" ("ColumnName", "Name", "Description", "Help", "IsActive")
            VALUES (@ColumnName, @Name, @Description, @Help, @IsActive)
            RETURNING "SysElement_ID"
            """;
        return connection.QuerySingle<int>(sql, new
        {
            entity.ColumnName,
            entity.Name,
            entity.Description,
            entity.Help,
            entity.IsActive,
        });
    }

    public void Update(SysElement entity)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            UPDATE "SysElement"
            SET "ColumnName" = @ColumnName,
                "Name" = @Name,
                "Description" = @Description,
                "Help" = @Help,
                "IsActive" = @IsActive
            WHERE "SysElement_ID" = @SysElementId
            """;
        connection.Execute(sql, new
        {
            entity.SysElementId,
            entity.ColumnName,
            entity.Name,
            entity.Description,
            entity.Help,
            entity.IsActive,
        });
    }

    public void Delete(int id)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = "DELETE FROM \"SysElement\" WHERE \"SysElement_ID\" = @Id";
        connection.Execute(sql, new { Id = id });
    }
}
