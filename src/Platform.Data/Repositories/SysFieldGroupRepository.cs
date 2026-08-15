using Dapper;
using Platform.Core.Metadata;

namespace Platform.Data.Repositories;

public class SysFieldGroupRepository : ISysRepository<SysFieldGroup>
{
    private readonly string _connectionString;

    public SysFieldGroupRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public SysFieldGroup? GetById(int id)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysFieldGroup_ID", "SysTab_ID" AS "SysTabId",
                   "ColumnName", "Name", "SeqNo", "ColSpan", "IsCollapsed",
                   "EntityType", "IsActive",
                   "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt"
            FROM "SysFieldGroup"
            WHERE "SysFieldGroup_ID" = @Id
            """;
        return connection.QueryFirstOrDefault<SysFieldGroup>(sql, new { Id = id });
    }

    public IEnumerable<SysFieldGroup> GetAll()
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysFieldGroup_ID", "SysTab_ID" AS "SysTabId",
                   "ColumnName", "Name", "SeqNo", "ColSpan", "IsCollapsed",
                   "EntityType", "IsActive",
                   "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt"
            FROM "SysFieldGroup"
            WHERE "IsActive" = true
            ORDER BY "SeqNo"
            """;
        return connection.Query<SysFieldGroup>(sql);
    }

    public IEnumerable<SysFieldGroup> GetByTabId(int tabId)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysFieldGroup_ID", "SysTab_ID" AS "SysTabId",
                   "ColumnName", "Name", "SeqNo", "ColSpan", "IsCollapsed",
                   "EntityType", "IsActive",
                   "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt"
            FROM "SysFieldGroup"
            WHERE "SysTab_ID" = @TabId AND "IsActive" = true
            ORDER BY "SeqNo"
            """;
        return connection.Query<SysFieldGroup>(sql, new { TabId = tabId });
    }

    public int Create(SysFieldGroup entity)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            INSERT INTO "SysFieldGroup"
                ("SysTab_ID", "ColumnName", "Name", "SeqNo", "ColSpan", "IsCollapsed",
                 "EntityType", "IsActive", "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt")
            VALUES (@SysTabId, @ColumnName, @Name, @SeqNo, @ColSpan, @IsCollapsed,
                    @EntityType, @IsActive, @CreatedBy, @CreatedAt, @UpdatedBy, @UpdatedAt)
            RETURNING "SysFieldGroup_ID"
            """;
        return connection.QuerySingle<int>(sql, new
        {
            entity.SysTabId,
            entity.ColumnName,
            entity.Name,
            entity.SeqNo,
            entity.ColSpan,
            entity.IsCollapsed,
            entity.EntityType,
            entity.IsActive,
            entity.CreatedBy,
            entity.CreatedAt,
            entity.UpdatedBy,
            entity.UpdatedAt,
        });
    }

    public void Update(SysFieldGroup entity)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            UPDATE "SysFieldGroup"
            SET "SysTab_ID" = @SysTabId,
                "ColumnName" = @ColumnName,
                "Name" = @Name,
                "SeqNo" = @SeqNo,
                "ColSpan" = @ColSpan,
                "IsCollapsed" = @IsCollapsed,
                "EntityType" = @EntityType,
                "IsActive" = @IsActive,
                "UpdatedBy" = @UpdatedBy,
                "UpdatedAt" = @UpdatedAt
            WHERE "SysFieldGroup_ID" = @SysFieldGroupId
            """;
        connection.Execute(sql, new
        {
            entity.SysFieldGroupId,
            entity.SysTabId,
            entity.ColumnName,
            entity.Name,
            entity.SeqNo,
            entity.ColSpan,
            entity.IsCollapsed,
            entity.EntityType,
            entity.IsActive,
            entity.UpdatedBy,
            entity.UpdatedAt,
        });
    }

    public void Delete(int id)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = "DELETE FROM \"SysFieldGroup\" WHERE \"SysFieldGroup_ID\" = @Id";
        connection.Execute(sql, new { Id = id });
    }
}
