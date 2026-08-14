using Dapper;
using Platform.Core.Metadata;

namespace Platform.Data.Repositories;

public class SysReferenceRepository : ISysRepository<SysReference>
{
    private readonly string _connectionString;

    public SysReferenceRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public SysReference? GetById(int id)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysReference_ID", "Name", "ValidationType", "IsSystemType", "ValueFormat"
            FROM "SysReference"
            WHERE "SysReference_ID" = @Id
            """;
        return connection.QueryFirstOrDefault<SysReference>(sql, new { Id = id });
    }

    public SysReference? GetByName(string name)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysReference_ID", "Name", "ValidationType", "IsSystemType", "ValueFormat"
            FROM "SysReference"
            WHERE "Name" = @Name
            """;
        return connection.QueryFirstOrDefault<SysReference>(sql, new { Name = name });
    }

    public IEnumerable<SysReference> GetAll()
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysReference_ID", "Name", "ValidationType", "IsSystemType", "ValueFormat"
            FROM "SysReference"
            ORDER BY "Name"
            """;
        return connection.Query<SysReference>(sql);
    }

    public int Create(SysReference entity)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            INSERT INTO "SysReference" ("Name", "ValidationType", "IsSystemType", "ValueFormat")
            VALUES (@Name, @ValidationType, @IsSystemType, @ValueFormat)
            RETURNING "SysReference_ID"
            """;
        return connection.QuerySingle<int>(sql, new
        {
            entity.Name,
            ValidationType = entity.ValidationType.ToString(),
            entity.IsSystemType,
            entity.ValueFormat,
        });
    }

    public void Update(SysReference entity)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            UPDATE "SysReference"
            SET "Name" = @Name,
                "ValidationType" = @ValidationType,
                "IsSystemType" = @IsSystemType,
                "ValueFormat" = @ValueFormat
            WHERE "SysReference_ID" = @SysReferenceId
            """;
        connection.Execute(sql, new
        {
            entity.SysReferenceId,
            entity.Name,
            ValidationType = entity.ValidationType.ToString(),
            entity.IsSystemType,
            entity.ValueFormat,
        });
    }

    public void Delete(int id)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = "DELETE FROM \"SysReference\" WHERE \"SysReference_ID\" = @Id";
        connection.Execute(sql, new { Id = id });
    }
}
