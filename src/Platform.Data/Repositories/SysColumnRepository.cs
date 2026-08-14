using Dapper;
using Platform.Core.Metadata;

namespace Platform.Data.Repositories;

public class SysColumnRepository : ISysRepository<SysColumn>
{
    private readonly string _connectionString;

    public SysColumnRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public SysColumn? GetById(int id)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysColumn_ID", "SysTable_ID", "ColumnName", "SysElement_ID",
                   "SysReference_ID", "SysValRule_ID", "SysReferenceValue_ID",
                   "FieldLength", "IsMandatory", "IsKey", "IsParent", "IsIdentifier",
                   "IsSelectionColumn", "IsEncrypted", "IsUpdateable",
                   "IsAlwaysUpdateable", "DefaultValue", "ValueMin", "ValueMax",
                   "SeqNo", "EntityType", "IsActive"
            FROM "SysColumn"
            WHERE "SysColumn_ID" = @Id
            """;
        return connection.QueryFirstOrDefault<SysColumn>(sql, new { Id = id });
    }

    public IEnumerable<SysColumn> GetByTableId(int sysTableId)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysColumn_ID", "SysTable_ID", "ColumnName", "SysElement_ID",
                   "SysReference_ID", "SysValRule_ID", "SysReferenceValue_ID",
                   "FieldLength", "IsMandatory", "IsKey", "IsParent", "IsIdentifier",
                   "IsSelectionColumn", "IsEncrypted", "IsUpdateable",
                   "IsAlwaysUpdateable", "DefaultValue", "ValueMin", "ValueMax",
                   "SeqNo", "EntityType", "IsActive"
            FROM "SysColumn"
            WHERE "SysTable_ID" = @SysTableId
            ORDER BY "SeqNo"
            """;
        return connection.Query<SysColumn>(sql, new { SysTableId = sysTableId });
    }

    public IEnumerable<SysColumn> GetAll()
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysColumn_ID", "SysTable_ID", "ColumnName", "SysElement_ID",
                   "SysReference_ID", "SysValRule_ID", "SysReferenceValue_ID",
                   "FieldLength", "IsMandatory", "IsKey", "IsParent", "IsIdentifier",
                   "IsSelectionColumn", "IsEncrypted", "IsUpdateable",
                   "IsAlwaysUpdateable", "DefaultValue", "ValueMin", "ValueMax",
                   "SeqNo", "EntityType", "IsActive"
            FROM "SysColumn"
            ORDER BY "SysTable_ID", "SeqNo"
            """;
        return connection.Query<SysColumn>(sql);
    }

    public int Create(SysColumn entity)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            INSERT INTO "SysColumn"
                ("SysTable_ID", "ColumnName", "SysElement_ID", "SysReference_ID",
                 "SysValRule_ID", "SysReferenceValue_ID", "FieldLength",
                 "IsMandatory", "IsKey", "IsParent", "IsIdentifier",
                 "IsSelectionColumn", "IsEncrypted", "IsUpdateable",
                 "IsAlwaysUpdateable", "DefaultValue", "ValueMin", "ValueMax",
                 "SeqNo", "EntityType", "IsActive")
            VALUES (@SysTableId, @ColumnName, @SysElementId, @SysReferenceId,
                    @SysValRuleId, @SysReferenceValueId, @FieldLength,
                    @IsMandatory, @IsKey, @IsParent, @IsIdentifier,
                    @IsSelectionColumn, @IsEncrypted, @IsUpdateable,
                    @IsAlwaysUpdateable, @DefaultValue, @ValueMin, @ValueMax,
                    @SeqNo, @EntityType, @IsActive)
            RETURNING "SysColumn_ID"
            """;
        return connection.QuerySingle<int>(sql, new
        {
            entity.SysTableId,
            entity.ColumnName,
            entity.SysElementId,
            entity.SysReferenceId,
            entity.SysValRuleId,
            entity.SysReferenceValueId,
            entity.FieldLength,
            entity.IsMandatory,
            entity.IsKey,
            entity.IsParent,
            entity.IsIdentifier,
            entity.IsSelectionColumn,
            entity.IsEncrypted,
            entity.IsUpdateable,
            entity.IsAlwaysUpdateable,
            entity.DefaultValue,
            entity.ValueMin,
            entity.ValueMax,
            entity.SeqNo,
            entity.EntityType,
            entity.IsActive,
        });
    }

    public void Update(SysColumn entity)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            UPDATE "SysColumn"
            SET "ColumnName" = @ColumnName,
                "SysElement_ID" = @SysElementId,
                "SysReference_ID" = @SysReferenceId,
                "SysValRule_ID" = @SysValRuleId,
                "SysReferenceValue_ID" = @SysReferenceValueId,
                "FieldLength" = @FieldLength,
                "IsMandatory" = @IsMandatory,
                "IsKey" = @IsKey,
                "IsParent" = @IsParent,
                "IsIdentifier" = @IsIdentifier,
                "IsSelectionColumn" = @IsSelectionColumn,
                "IsEncrypted" = @IsEncrypted,
                "IsUpdateable" = @IsUpdateable,
                "IsAlwaysUpdateable" = @IsAlwaysUpdateable,
                "DefaultValue" = @DefaultValue,
                "ValueMin" = @ValueMin,
                "ValueMax" = @ValueMax,
                "SeqNo" = @SeqNo,
                "EntityType" = @EntityType,
                "IsActive" = @IsActive
            WHERE "SysColumn_ID" = @SysColumnId
            """;
        connection.Execute(sql, new
        {
            entity.SysColumnId,
            entity.ColumnName,
            entity.SysElementId,
            entity.SysReferenceId,
            entity.SysValRuleId,
            entity.SysReferenceValueId,
            entity.FieldLength,
            entity.IsMandatory,
            entity.IsKey,
            entity.IsParent,
            entity.IsIdentifier,
            entity.IsSelectionColumn,
            entity.IsEncrypted,
            entity.IsUpdateable,
            entity.IsAlwaysUpdateable,
            entity.DefaultValue,
            entity.ValueMin,
            entity.ValueMax,
            entity.SeqNo,
            entity.EntityType,
            entity.IsActive,
        });
    }

    public void Delete(int id)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = "DELETE FROM \"SysColumn\" WHERE \"SysColumn_ID\" = @Id";
        connection.Execute(sql, new { Id = id });
    }
}
