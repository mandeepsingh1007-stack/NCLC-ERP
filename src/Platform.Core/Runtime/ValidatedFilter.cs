using Npgsql;

namespace Platform.Core.Runtime;

/// <summary>
/// Result of filter parsing and validation — contains parameterized SQL fragment (ADR-0007).
/// </summary>
public sealed class ValidatedFilter
{
    public string SqlWhereClause { get; }
    public NpgsqlParameter[] Parameters { get; }
    public int ClauseCount { get; }

    public ValidatedFilter(string sqlWhereClause, NpgsqlParameter[] parameters, int clauseCount)
    {
        SqlWhereClause = sqlWhereClause;
        Parameters = parameters;
        ClauseCount = clauseCount;
    }
}
