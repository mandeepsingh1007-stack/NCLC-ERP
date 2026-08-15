using Dapper;
using Platform.Core.Metadata;

namespace Platform.Data.Repositories;

public class SysTabRepository : ISysRepository<SysTab>
{
    private readonly string _connectionString;

    public SysTabRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public SysTab? GetById(int id)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysTab_ID", "SysWindow_ID" AS "SysWindowId", "SysTable_ID" AS "SysTableId",
                   "ColumnName", "Name", "SeqNo", "IsDefaultTab", "IsGrid",
                   "WhereClause", "IsDeleteable", "EntityType", "IsActive",
                   "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt"
            FROM "SysTab"
            WHERE "SysTab_ID" = @Id
            """;
        return connection.QueryFirstOrDefault<SysTab>(sql, new { Id = id });
    }

    public IEnumerable<SysTab> GetAll()
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysTab_ID", "SysWindow_ID" AS "SysWindowId", "SysTable_ID" AS "SysTableId",
                   "ColumnName", "Name", "SeqNo", "IsDefaultTab", "IsGrid",
                   "WhereClause", "IsDeleteable", "EntityType", "IsActive",
                   "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt"
            FROM "SysTab"
            WHERE "IsActive" = true
            ORDER BY "SeqNo"
            """;
        return connection.Query<SysTab>(sql);
    }

    public IEnumerable<SysTab> GetByWindowId(int windowId)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysTab_ID", "SysWindow_ID" AS "SysWindowId", "SysTable_ID" AS "SysTableId",
                   "ColumnName", "Name", "SeqNo", "IsDefaultTab", "IsGrid",
                   "WhereClause", "IsDeleteable", "EntityType", "IsActive",
                   "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt"
            FROM "SysTab"
            WHERE "SysWindow_ID" = @WindowId AND "IsActive" = true
            ORDER BY "SeqNo"
            """;
        return connection.Query<SysTab>(sql, new { WindowId = windowId });
    }

    public int Create(SysTab entity)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            INSERT INTO "SysTab"
                ("SysWindow_ID", "SysTable_ID", "ColumnName", "Name", "SeqNo",
                 "IsDefaultTab", "IsGrid", "WhereClause", "IsDeleteable",
                 "EntityType", "IsActive", "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt")
            VALUES (@SysWindowId, @SysTableId, @ColumnName, @Name, @SeqNo,
                    @IsDefaultTab, @IsGrid, @WhereClause, @IsDeleteable,
                    @EntityType, @IsActive, @CreatedBy, @CreatedAt, @UpdatedBy, @UpdatedAt)
            RETURNING "SysTab_ID"
            """;
        return connection.QuerySingle<int>(sql, new
        {
            entity.SysWindowId,
            entity.SysTableId,
            entity.ColumnName,
            entity.Name,
            entity.SeqNo,
            entity.IsDefaultTab,
            entity.IsGrid,
            entity.WhereClause,
            entity.IsDeleteable,
            entity.EntityType,
            entity.IsActive,
            entity.CreatedBy,
            entity.CreatedAt,
            entity.UpdatedBy,
            entity.UpdatedAt,
        });
    }

    public void Update(SysTab entity)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            UPDATE "SysTab"
            SET "SysWindow_ID" = @SysWindowId,
                "SysTable_ID" = @SysTableId,
                "ColumnName" = @ColumnName,
                "Name" = @Name,
                "SeqNo" = @SeqNo,
                "IsDefaultTab" = @IsDefaultTab,
                "IsGrid" = @IsGrid,
                "WhereClause" = @WhereClause,
                "IsDeleteable" = @IsDeleteable,
                "EntityType" = @EntityType,
                "IsActive" = @IsActive,
                "UpdatedBy" = @UpdatedBy,
                "UpdatedAt" = @UpdatedAt
            WHERE "SysTab_ID" = @SysTabId
            """;
        connection.Execute(sql, new
        {
            entity.SysTabId,
            entity.SysWindowId,
            entity.SysTableId,
            entity.ColumnName,
            entity.Name,
            entity.SeqNo,
            entity.IsDefaultTab,
            entity.IsGrid,
            entity.WhereClause,
            entity.IsDeleteable,
            entity.EntityType,
            entity.IsActive,
            entity.UpdatedBy,
            entity.UpdatedAt,
        });
    }

    public void Delete(int id)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = "DELETE FROM \"SysTab\" WHERE \"SysTab_ID\" = @Id";
        connection.Execute(sql, new { Id = id });
    }
}
