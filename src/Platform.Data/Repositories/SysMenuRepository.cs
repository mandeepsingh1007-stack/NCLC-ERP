using Dapper;
using Platform.Core.Metadata;

namespace Platform.Data.Repositories;

public class SysMenuRepository : ISysRepository<SysMenu>
{
    private readonly string _connectionString;

    public SysMenuRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public SysMenu? GetById(int id)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysMenu_ID", "Parent_ID" AS "ParentId", "ColumnName", "Name",
                   "Icon", "Sequence", "Window_ID" AS "WindowId", "Process_ID" AS "ProcessId",
                   "IsSeparator", "IsSystem", "EntityType", "IsActive",
                   "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt"
            FROM "SysMenu"
            WHERE "SysMenu_ID" = @Id
            """;
        return connection.QueryFirstOrDefault<SysMenu>(sql, new { Id = id });
    }

    public SysMenu? GetByColumnName(string columnName)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysMenu_ID", "Parent_ID" AS "ParentId", "ColumnName", "Name",
                   "Icon", "Sequence", "Window_ID" AS "WindowId", "Process_ID" AS "ProcessId",
                   "IsSeparator", "IsSystem", "EntityType", "IsActive",
                   "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt"
            FROM "SysMenu"
            WHERE "ColumnName" = @ColumnName
            """;
        return connection.QueryFirstOrDefault<SysMenu>(sql, new { ColumnName = columnName });
    }

    public IEnumerable<SysMenu> GetAll()
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysMenu_ID", "Parent_ID" AS "ParentId", "ColumnName", "Name",
                   "Icon", "Sequence", "Window_ID" AS "WindowId", "Process_ID" AS "ProcessId",
                   "IsSeparator", "IsSystem", "EntityType", "IsActive",
                   "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt"
            FROM "SysMenu"
            WHERE "IsActive" = true
            ORDER BY "Sequence"
            """;
        return connection.Query<SysMenu>(sql);
    }

    public IEnumerable<SysMenu> GetRootItems()
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysMenu_ID", "Parent_ID" AS "ParentId", "ColumnName", "Name",
                   "Icon", "Sequence", "Window_ID" AS "WindowId", "Process_ID" AS "ProcessId",
                   "IsSeparator", "IsSystem", "EntityType", "IsActive",
                   "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt"
            FROM "SysMenu"
            WHERE "Parent_ID" IS NULL AND "IsActive" = true
            ORDER BY "Sequence"
            """;
        return connection.Query<SysMenu>(sql);
    }

    public IEnumerable<SysMenu> GetChildren(int parentId)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysMenu_ID", "Parent_ID" AS "ParentId", "ColumnName", "Name",
                   "Icon", "Sequence", "Window_ID" AS "WindowId", "Process_ID" AS "ProcessId",
                   "IsSeparator", "IsSystem", "EntityType", "IsActive",
                   "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt"
            FROM "SysMenu"
            WHERE "Parent_ID" = @ParentId AND "IsActive" = true
            ORDER BY "Sequence"
            """;
        return connection.Query<SysMenu>(sql, new { ParentId = parentId });
    }

    public IEnumerable<SysMenu> GetByWindowId(int windowId)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysMenu_ID", "Parent_ID" AS "ParentId", "ColumnName", "Name",
                   "Icon", "Sequence", "Window_ID" AS "WindowId", "Process_ID" AS "ProcessId",
                   "IsSeparator", "IsSystem", "EntityType", "IsActive",
                   "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt"
            FROM "SysMenu"
            WHERE "Window_ID" = @WindowId AND "IsActive" = true
            ORDER BY "Sequence"
            """;
        return connection.Query<SysMenu>(sql, new { WindowId = windowId });
    }

    public IEnumerable<SysMenu> GetHierarchy()
    {
        // Load entire menu hierarchy in a single query
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysMenu_ID", "Parent_ID" AS "ParentId", "ColumnName", "Name",
                   "Icon", "Sequence", "Window_ID" AS "WindowId", "Process_ID" AS "ProcessId",
                   "IsSeparator", "IsSystem", "EntityType", "IsActive",
                   "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt"
            FROM "SysMenu"
            WHERE "IsActive" = true
            ORDER BY "Sequence", "SysMenu_ID"
            """;
        return connection.Query<SysMenu>(sql);
    }

    public int Create(SysMenu entity)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            INSERT INTO "SysMenu"
                ("Parent_ID", "ColumnName", "Name", "Icon", "Sequence",
                 "Window_ID", "Process_ID", "IsSeparator", "IsSystem",
                 "EntityType", "IsActive", "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt")
            VALUES (@ParentId, @ColumnName, @Name, @Icon, @Sequence,
                    @WindowId, @ProcessId, @IsSeparator, @IsSystem,
                    @EntityType, @IsActive, @CreatedBy, @CreatedAt, @UpdatedBy, @UpdatedAt)
            RETURNING "SysMenu_ID"
            """;
        return connection.QuerySingle<int>(sql, new
        {
            entity.ParentId,
            entity.ColumnName,
            entity.Name,
            entity.Icon,
            entity.Sequence,
            entity.WindowId,
            entity.ProcessId,
            entity.IsSeparator,
            entity.IsSystem,
            entity.EntityType,
            entity.IsActive,
            entity.CreatedBy,
            entity.CreatedAt,
            entity.UpdatedBy,
            entity.UpdatedAt,
        });
    }

    public void Update(SysMenu entity)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            UPDATE "SysMenu"
            SET "Parent_ID" = @ParentId,
                "ColumnName" = @ColumnName,
                "Name" = @Name,
                "Icon" = @Icon,
                "Sequence" = @Sequence,
                "Window_ID" = @WindowId,
                "Process_ID" = @ProcessId,
                "IsSeparator" = @IsSeparator,
                "IsSystem" = @IsSystem,
                "EntityType" = @EntityType,
                "IsActive" = @IsActive,
                "UpdatedBy" = @UpdatedBy,
                "UpdatedAt" = @UpdatedAt
            WHERE "SysMenu_ID" = @SysMenuId
            """;
        connection.Execute(sql, new
        {
            entity.SysMenuId,
            entity.ParentId,
            entity.ColumnName,
            entity.Name,
            entity.Icon,
            entity.Sequence,
            entity.WindowId,
            entity.ProcessId,
            entity.IsSeparator,
            entity.IsSystem,
            entity.EntityType,
            entity.IsActive,
            entity.UpdatedBy,
            entity.UpdatedAt,
        });
    }

    public void Delete(int id)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = "DELETE FROM \"SysMenu\" WHERE \"SysMenu_ID\" = @Id";
        connection.Execute(sql, new { Id = id });
    }
}
