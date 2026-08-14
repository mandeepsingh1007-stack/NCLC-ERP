using Dapper;
using Platform.Core.Metadata;

namespace Platform.Data.Repositories;

public class SysTranslationRepository : ISysRepository<SysTranslation>
{
    private readonly string _connectionString;

    public SysTranslationRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public SysTranslation? GetById(int id)
    {
        throw new NotImplementedException("Translations are queried by SysElementId + LanguageCode");
    }

    public IEnumerable<SysTranslation> GetAll()
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysElement_ID" AS "SysElementId", "Language" AS "LanguageCode",
                   "Name", "Description", "Help"
            FROM "SysElement_Trl"
            ORDER BY "SysElement_ID", "Language"
            """;
        return connection.Query<SysTranslation>(sql);
    }

    public IEnumerable<SysTranslation> GetByElementId(int sysElementId)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT "SysElement_ID" AS "SysElementId", "Language" AS "LanguageCode",
                   "Name", "Description", "Help"
            FROM "SysElement_Trl"
            WHERE "SysElement_ID" = @SysElementId
            """;
        return connection.Query<SysTranslation>(sql, new { SysElementId = sysElementId });
    }

    public int Create(SysTranslation entity)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            INSERT INTO "SysElement_Trl" ("SysElement_ID", "Language", "Name", "Description", "Help")
            VALUES (@SysElementId, @LanguageCode, @Name, @Description, @Help)
            ON CONFLICT ("SysElement_ID", "Language")
            DO UPDATE SET "Name" = EXCLUDED."Name",
                          "Description" = EXCLUDED."Description",
                          "Help" = EXCLUDED."Help"
            """;
        return connection.Execute(sql, new
        {
            entity.SysElementId,
            entity.LanguageCode,
            entity.Name,
            entity.Description,
            entity.Help,
        });
    }

    public void Update(SysTranslation entity)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            UPDATE "SysElement_Trl"
            SET "Name" = @Name,
                "Description" = @Description,
                "Help" = @Help
            WHERE "SysElement_ID" = @SysElementId
              AND "Language" = @LanguageCode
            """;
        connection.Execute(sql, new
        {
            entity.SysElementId,
            entity.LanguageCode,
            entity.Name,
            entity.Description,
            entity.Help,
        });
    }

    public void Delete(int id)
    {
        throw new NotImplementedException("Use DeleteByElementAndLanguage overload");
    }

    public void DeleteByElementAndLanguage(int sysElementId, string languageCode)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        const string sql = """
            DELETE FROM "SysElement_Trl"
            WHERE "SysElement_ID" = @SysElementId
              AND "Language" = @LanguageCode
            """;
        connection.Execute(sql, new { SysElementId = sysElementId, LanguageCode = languageCode });
    }
}
