using Dapper;
using Platform.Core.Metadata;

namespace Platform.Data.Repositories;

public class SysValRuleRepository : ISysRepository<SysValRule>
{
    private readonly string _connectionString;

    public SysValRuleRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public SysValRule? GetById(int id)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysValRule_ID", "Name", "Description", "RuleType", "Code", "IsActive"
            FROM "SysValRule"
            WHERE "SysValRule_ID" = @Id
            """;
        return connection.QueryFirstOrDefault<SysValRule>(sql, new { Id = id });
    }

    public SysValRule? GetByName(string name)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysValRule_ID", "Name", "Description", "RuleType", "Code", "IsActive"
            FROM "SysValRule"
            WHERE "Name" = @Name
            """;
        return connection.QueryFirstOrDefault<SysValRule>(sql, new { Name = name });
    }

    public IEnumerable<SysValRule> GetAll()
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysValRule_ID", "Name", "Description", "RuleType", "Code", "IsActive"
            FROM "SysValRule"
            ORDER BY "Name"
            """;
        return connection.Query<SysValRule>(sql);
    }

    public IEnumerable<SysValRule> GetActiveRules()
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysValRule_ID", "Name", "Description", "RuleType", "Code", "IsActive"
            FROM "SysValRule"
            WHERE "IsActive" = TRUE
            ORDER BY "Name"
            """;
        return connection.Query<SysValRule>(sql);
    }

    public int Create(SysValRule entity)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            INSERT INTO "SysValRule" ("Name", "Description", "RuleType", "Code", "IsActive")
            VALUES (@Name, @Description, @RuleType, @Code, @IsActive)
            RETURNING "SysValRule_ID"
            """;
        return connection.QuerySingle<int>(sql, new
        {
            entity.Name,
            entity.Description,
            RuleType = entity.RuleType.ToString(),
            entity.Code,
            entity.IsActive,
        });
    }

    public void Update(SysValRule entity)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            UPDATE "SysValRule"
            SET "Name" = @Name,
                "Description" = @Description,
                "RuleType" = @RuleType,
                "Code" = @Code,
                "IsActive" = @IsActive
            WHERE "SysValRule_ID" = @SysValRuleId
            """;
        connection.Execute(sql, new
        {
            entity.SysValRuleId,
            entity.Name,
            entity.Description,
            RuleType = entity.RuleType.ToString(),
            entity.Code,
            entity.IsActive,
        });
    }

    public void Delete(int id)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = "DELETE FROM \"SysValRule\" WHERE \"SysValRule_ID\" = @Id";
        connection.Execute(sql, new { Id = id });
    }
}
