using Dapper;
using Platform.Core.Metadata;

namespace Platform.Data.Repositories;

public class SysFieldRepository : ISysRepository<SysField>
{
    private readonly string _connectionString;

    public SysFieldRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public SysField? GetById(int id)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysField_ID", "SysTab_ID" AS "SysTabId", "SysColumn_ID" AS "SysColumnId",
                   "ColumnName", "Name", "ControlType",
                   "SysFieldGroup_ID" AS "SysFieldGroupId", "SeqNo",
                   "IsMandatoryOverride", "IsReadOnlyOverride",
                   "ColSpan", "RowSpan",
                   "DisplayLogic", "ReadOnlyLogic", "MandatoryLogic",
                   "DefaultValue", "EntityType", "IsActive",
                   "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt"
            FROM "SysField"
            WHERE "SysField_ID" = @Id
            """;
        return connection.QueryFirstOrDefault<SysField>(sql, new { Id = id });
    }

    public IEnumerable<SysField> GetAll()
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysField_ID", "SysTab_ID" AS "SysTabId", "SysColumn_ID" AS "SysColumnId",
                   "ColumnName", "Name", "ControlType",
                   "SysFieldGroup_ID" AS "SysFieldGroupId", "SeqNo",
                   "IsMandatoryOverride", "IsReadOnlyOverride",
                   "ColSpan", "RowSpan",
                   "DisplayLogic", "ReadOnlyLogic", "MandatoryLogic",
                   "DefaultValue", "EntityType", "IsActive",
                   "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt"
            FROM "SysField"
            WHERE "IsActive" = true
            ORDER BY "SeqNo"
            """;
        return connection.Query<SysField>(sql);
    }

    public IEnumerable<SysField> GetByTabId(int tabId)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysField_ID", "SysTab_ID" AS "SysTabId", "SysColumn_ID" AS "SysColumnId",
                   "ColumnName", "Name", "ControlType",
                   "SysFieldGroup_ID" AS "SysFieldGroupId", "SeqNo",
                   "IsMandatoryOverride", "IsReadOnlyOverride",
                   "ColSpan", "RowSpan",
                   "DisplayLogic", "ReadOnlyLogic", "MandatoryLogic",
                   "DefaultValue", "EntityType", "IsActive",
                   "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt"
            FROM "SysField"
            WHERE "SysTab_ID" = @TabId AND "IsActive" = true
            ORDER BY "SeqNo"
            """;
        return connection.Query<SysField>(sql, new { TabId = tabId });
    }

    public IEnumerable<SysField> GetByTabAndGroupId(int tabId, int? groupId)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        var sql = """
            SELECT "SysField_ID", "SysTab_ID" AS "SysTabId", "SysColumn_ID" AS "SysColumnId",
                   "ColumnName", "Name", "ControlType",
                   "SysFieldGroup_ID" AS "SysFieldGroupId", "SeqNo",
                   "IsMandatoryOverride", "IsReadOnlyOverride",
                   "ColSpan", "RowSpan",
                   "DisplayLogic", "ReadOnlyLogic", "MandatoryLogic",
                   "DefaultValue", "EntityType", "IsActive",
                   "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt"
            FROM "SysField"
            WHERE "SysTab_ID" = @TabId AND "IsActive" = true
            """;

        if (groupId.HasValue)
        {
            sql += " AND \"SysFieldGroup_ID\" = @GroupId";
            return connection.Query<SysField>(sql, new { TabId = tabId, GroupId = groupId.Value })
                .ToList().AsReadOnly();
        }
        else
        {
            sql += " AND \"SysFieldGroup_ID\" IS NULL";
            return connection.Query<SysField>(sql, new { TabId = tabId })
                .ToList().AsReadOnly();
        }
    }

    public int Create(SysField entity)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            INSERT INTO "SysField"
                ("SysTab_ID", "SysColumn_ID", "ColumnName", "Name", "ControlType",
                 "SysFieldGroup_ID", "SeqNo", "IsMandatoryOverride", "IsReadOnlyOverride",
                 "ColSpan", "RowSpan", "DisplayLogic", "ReadOnlyLogic", "MandatoryLogic",
                 "DefaultValue", "EntityType", "IsActive",
                 "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt")
            VALUES (@SysTabId, @SysColumnId, @ColumnName, @Name, @ControlType,
                    @SysFieldGroupId, @SeqNo, @IsMandatoryOverride, @IsReadOnlyOverride,
                    @ColSpan, @RowSpan, @DisplayLogic, @ReadOnlyLogic, @MandatoryLogic,
                    @DefaultValue, @EntityType, @IsActive,
                    @CreatedBy, @CreatedAt, @UpdatedBy, @UpdatedAt)
            RETURNING "SysField_ID"
            """;
        return connection.QuerySingle<int>(sql, new
        {
            entity.SysTabId,
            entity.SysColumnId,
            entity.ColumnName,
            entity.Name,
            entity.ControlType,
            entity.SysFieldGroupId,
            entity.SeqNo,
            entity.IsMandatoryOverride,
            entity.IsReadOnlyOverride,
            entity.ColSpan,
            entity.RowSpan,
            entity.DisplayLogic,
            entity.ReadOnlyLogic,
            entity.MandatoryLogic,
            entity.DefaultValue,
            entity.EntityType,
            entity.IsActive,
            entity.CreatedBy,
            entity.CreatedAt,
            entity.UpdatedBy,
            entity.UpdatedAt,
        });
    }

    public void Update(SysField entity)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            UPDATE "SysField"
            SET "SysTab_ID" = @SysTabId,
                "SysColumn_ID" = @SysColumnId,
                "ColumnName" = @ColumnName,
                "Name" = @Name,
                "ControlType" = @ControlType,
                "SysFieldGroup_ID" = @SysFieldGroupId,
                "SeqNo" = @SeqNo,
                "IsMandatoryOverride" = @IsMandatoryOverride,
                "IsReadOnlyOverride" = @IsReadOnlyOverride,
                "ColSpan" = @ColSpan,
                "RowSpan" = @RowSpan,
                "DisplayLogic" = @DisplayLogic,
                "ReadOnlyLogic" = @ReadOnlyLogic,
                "MandatoryLogic" = @MandatoryLogic,
                "DefaultValue" = @DefaultValue,
                "EntityType" = @EntityType,
                "IsActive" = @IsActive,
                "UpdatedBy" = @UpdatedBy,
                "UpdatedAt" = @UpdatedAt
            WHERE "SysField_ID" = @SysFieldId
            """;
        connection.Execute(sql, new
        {
            entity.SysFieldId,
            entity.SysTabId,
            entity.SysColumnId,
            entity.ColumnName,
            entity.Name,
            entity.ControlType,
            entity.SysFieldGroupId,
            entity.SeqNo,
            entity.IsMandatoryOverride,
            entity.IsReadOnlyOverride,
            entity.ColSpan,
            entity.RowSpan,
            entity.DisplayLogic,
            entity.ReadOnlyLogic,
            entity.MandatoryLogic,
            entity.DefaultValue,
            entity.EntityType,
            entity.IsActive,
            entity.UpdatedBy,
            entity.UpdatedAt,
        });
    }

    public void Delete(int id)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = "DELETE FROM \"SysField\" WHERE \"SysField_ID\" = @Id";
        connection.Execute(sql, new { Id = id });
    }
}
