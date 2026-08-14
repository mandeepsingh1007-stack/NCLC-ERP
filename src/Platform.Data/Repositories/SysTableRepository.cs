using Dapper;
using Platform.Core.Metadata;

namespace Platform.Data.Repositories;

public class SysTableRepository : ISysRepository<SysTable>
{
    private readonly string _connectionString;

    public SysTableRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public SysTable? GetById(int id)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysTable_ID", "TableName", "ClassName", "Description",
                   "IsView", "AccessLevel", "IsChangeLog", "IsDeleteable",
                   "IsHighVolume", "ReplicationType", "SysWindow_ID" AS "SysWindowId",
                   "EntityType", "IsActive"
            FROM "SysTable"
            WHERE "SysTable_ID" = @Id
            """;
        return connection.QueryFirstOrDefault<SysTable>(sql, new { Id = id });
    }

    public SysTable? GetByName(string tableName)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysTable_ID", "TableName", "ClassName", "Description",
                   "IsView", "AccessLevel", "IsChangeLog", "IsDeleteable",
                   "IsHighVolume", "ReplicationType", "SysWindow_ID" AS "SysWindowId",
                   "EntityType", "IsActive"
            FROM "SysTable"
            WHERE "TableName" = @TableName
            """;
        return connection.QueryFirstOrDefault<SysTable>(sql, new { TableName = tableName });
    }

    public IEnumerable<SysTable> GetAll()
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysTable_ID", "TableName", "ClassName", "Description",
                   "IsView", "AccessLevel", "IsChangeLog", "IsDeleteable",
                   "IsHighVolume", "ReplicationType", "SysWindow_ID" AS "SysWindowId",
                   "EntityType", "IsActive"
            FROM "SysTable"
            ORDER BY "TableName"
            """;
        return connection.Query<SysTable>(sql);
    }

    public IEnumerable<SysTable> GetByEntityType(string entityType)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysTable_ID", "TableName", "ClassName", "Description",
                   "IsView", "AccessLevel", "IsChangeLog", "IsDeleteable",
                   "IsHighVolume", "ReplicationType", "SysWindow_ID" AS "SysWindowId",
                   "EntityType", "IsActive"
            FROM "SysTable"
            WHERE "EntityType" = @EntityType
            ORDER BY "TableName"
            """;
        return connection.Query<SysTable>(sql, new { EntityType = entityType });
    }

    public int Create(SysTable entity)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            INSERT INTO "SysTable"
                ("TableName", "ClassName", "Description", "IsView", "AccessLevel",
                 "IsChangeLog", "IsDeleteable", "IsHighVolume", "ReplicationType",
                 "SysWindow_ID", "EntityType", "IsActive")
            VALUES (@TableName, @ClassName, @Description, @IsView, @AccessLevel,
                    @IsChangeLog, @IsDeleteable, @IsHighVolume, @ReplicationType,
                    @SysWindowId, @EntityType, @IsActive)
            RETURNING "SysTable_ID"
            """;
        return connection.QuerySingle<int>(sql, new
        {
            entity.TableName,
            entity.ClassName,
            entity.Description,
            entity.IsView,
            entity.AccessLevel,
            entity.IsChangeLog,
            entity.IsDeleteable,
            entity.IsHighVolume,
            entity.ReplicationType,
            entity.SysWindowId,
            entity.EntityType,
            entity.IsActive,
        });
    }

    public void Update(SysTable entity)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            UPDATE "SysTable"
            SET "TableName" = @TableName,
                "ClassName" = @ClassName,
                "Description" = @Description,
                "IsView" = @IsView,
                "AccessLevel" = @AccessLevel,
                "IsChangeLog" = @IsChangeLog,
                "IsDeleteable" = @IsDeleteable,
                "IsHighVolume" = @IsHighVolume,
                "ReplicationType" = @ReplicationType,
                "SysWindow_ID" = @SysWindowId,
                "EntityType" = @EntityType,
                "IsActive" = @IsActive
            WHERE "SysTable_ID" = @SysTableId
            """;
        connection.Execute(sql, new
        {
            entity.SysTableId,
            entity.TableName,
            entity.ClassName,
            entity.Description,
            entity.IsView,
            entity.AccessLevel,
            entity.IsChangeLog,
            entity.IsDeleteable,
            entity.IsHighVolume,
            entity.ReplicationType,
            entity.SysWindowId,
            entity.EntityType,
            entity.IsActive,
        });
    }

    public void Delete(int id)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = "DELETE FROM \"SysTable\" WHERE \"SysTable_ID\" = @Id";
        connection.Execute(sql, new { Id = id });
    }
}
