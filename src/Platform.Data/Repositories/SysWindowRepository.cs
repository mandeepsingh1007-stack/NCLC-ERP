using Dapper;
using Platform.Core.Metadata;

namespace Platform.Data.Repositories;

public class SysWindowRepository : ISysRepository<SysWindow>
{
    private readonly string _connectionString;

    public SysWindowRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public SysWindow? GetById(int id)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysWindow_ID", "ColumnName", "Name", "Description", "Help",
                   "DefaultTab_ID" AS "DefaultTabId", "AccessLevel", "IsView",
                   "EntityType", "IsActive", "CreatedBy", "CreatedAt",
                   "UpdatedBy", "UpdatedAt"
            FROM "SysWindow"
            WHERE "SysWindow_ID" = @Id
            """;
        return connection.QueryFirstOrDefault<SysWindow>(sql, new { Id = id });
    }

    public SysWindow? GetByColumnName(string columnName)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysWindow_ID", "ColumnName", "Name", "Description", "Help",
                   "DefaultTab_ID" AS "DefaultTabId", "AccessLevel", "IsView",
                   "EntityType", "IsActive", "CreatedBy", "CreatedAt",
                   "UpdatedBy", "UpdatedAt"
            FROM "SysWindow"
            WHERE "ColumnName" = @ColumnName
            """;
        return connection.QueryFirstOrDefault<SysWindow>(sql, new { ColumnName = columnName });
    }

    public IEnumerable<SysWindow> GetAll()
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysWindow_ID", "ColumnName", "Name", "Description", "Help",
                   "DefaultTab_ID" AS "DefaultTabId", "AccessLevel", "IsView",
                   "EntityType", "IsActive", "CreatedBy", "CreatedAt",
                   "UpdatedBy", "UpdatedAt"
            FROM "SysWindow"
            WHERE "IsActive" = true
            ORDER BY "AccessLevel", "ColumnName"
            """;
        return connection.Query<SysWindow>(sql);
    }

    public IEnumerable<SysWindow> GetAllActive()
    {
        return GetAll();
    }

    public int Create(SysWindow entity)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            INSERT INTO "SysWindow"
                ("ColumnName", "Name", "Description", "Help", "DefaultTab_ID",
                 "AccessLevel", "IsView", "EntityType", "IsActive",
                 "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt")
            VALUES (@ColumnName, @Name, @Description, @Help, @DefaultTabId,
                    @AccessLevel, @IsView, @EntityType, @IsActive,
                    @CreatedBy, @CreatedAt, @UpdatedBy, @UpdatedAt)
            RETURNING "SysWindow_ID"
            """;
        return connection.QuerySingle<int>(sql, new
        {
            entity.ColumnName,
            entity.Name,
            entity.Description,
            entity.Help,
            entity.DefaultTabId,
            entity.AccessLevel,
            entity.IsView,
            entity.EntityType,
            entity.IsActive,
            entity.CreatedBy,
            entity.CreatedAt,
            entity.UpdatedBy,
            entity.UpdatedAt,
        });
    }

    public void Update(SysWindow entity)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            UPDATE "SysWindow"
            SET "ColumnName" = @ColumnName,
                "Name" = @Name,
                "Description" = @Description,
                "Help" = @Help,
                "DefaultTab_ID" = @DefaultTabId,
                "AccessLevel" = @AccessLevel,
                "IsView" = @IsView,
                "EntityType" = @EntityType,
                "IsActive" = @IsActive,
                "UpdatedBy" = @UpdatedBy,
                "UpdatedAt" = @UpdatedAt
            WHERE "SysWindow_ID" = @SysWindowId
            """;
        connection.Execute(sql, new
        {
            entity.SysWindowId,
            entity.ColumnName,
            entity.Name,
            entity.Description,
            entity.Help,
            entity.DefaultTabId,
            entity.AccessLevel,
            entity.IsView,
            entity.EntityType,
            entity.IsActive,
            entity.UpdatedBy,
            entity.UpdatedAt,
        });
    }

    public void Delete(int id)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = "DELETE FROM \"SysWindow\" WHERE \"SysWindow_ID\" = @Id";
        connection.Execute(sql, new { Id = id });
    }
}
