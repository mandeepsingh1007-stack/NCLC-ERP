using System.Collections.Generic;
using System.Text;
using Dapper;
using Npgsql;
using Platform.Core.Metadata;

namespace Platform.Core.Runtime;

/// <summary>
/// Builds parameterized SQL queries for generic data CRUD.
/// Enforces 3-layer SQL injection defense:
///   1. Table allowlist (from MetadataGraph)
///   2. Column allowlist (from MetadataGraph)
///   3. All values via NpgsqlParameter[]
///
/// Table/column identifiers are double-quoted per PostgreSQL rules.
/// Tenant/Org predicates are injected from IReadOnlyContext.
/// </summary>
public class QueryBuilder
{
    private readonly IMetadataGraph _metadataGraph;
    private const int MaxPageSize = 500;
    private const int DefaultPageSize = 50;
    private const int MinPageSize = 1;

    public QueryBuilder(IMetadataGraph metadataGraph)
    {
        _metadataGraph = metadataGraph;
    }

    /// <summary>
    /// Validates a table name against the metadata graph.
    /// Returns the quoted identifier or null if not found.
    /// </summary>
    public string? ValidateTable(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            return null;

        var meta = _metadataGraph.GetTable(tableName);
        return meta != null ? $"\"{meta.TableName}\"" : null;
    }

    /// <summary>
    /// Validates a column name against the metadata graph for a given table.
    /// Returns the quoted identifier or null if not found.
    /// </summary>
    public string? ValidateColumn(string tableName, string columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName))
            return null;

        var columns = _metadataGraph.GetColumns(tableName);
        var match = columns.FirstOrDefault(c =>
            string.Equals(c.ColumnName, columnName, StringComparison.OrdinalIgnoreCase));
        return match != null ? $"\"{match.ColumnName}\"" : null;
    }

    /// <summary>
    /// Validates a list of column names for projection (SELECT clause).
    /// Returns quoted identifiers or null if any are invalid.
    /// </summary>
    public IReadOnlyList<string>? ValidateColumns(string tableName, IEnumerable<string> columnNames)
    {
        var result = new List<string>();
        foreach (var col in columnNames)
        {
            var validated = ValidateColumn(tableName, col);
            if (validated == null)
                return null;
            result.Add(validated);
        }
        return result;
    }

    /// <summary>
    /// Validates sort column and direction.
    /// Returns (quoted_column, direction) or null if invalid.
    /// </summary>
    public (string Column, string Direction)? ValidateSort(string tableName, string? sortBy, string? sortDir)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
            return null;

        var quoted = ValidateColumn(tableName, sortBy);
        if (quoted == null)
            return null;

        var dir = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        return (quoted, dir);
    }

    /// <summary>
    /// Builds a parameterized SELECT query for generic grid listing.
    /// Includes tenant/org predicates from context.
    /// Returns (sql, parameters, countSql) tuple.
    /// </summary>
    public (string Sql, object Parameters, string CountSql) BuildSelect(
        string tableName,
        IReadOnlyContext context,
        int page = 1,
        int pageSize = DefaultPageSize,
        string? sortBy = null,
        string? sortDir = null,
        string? filterSql = null,
        NpgsqlParameter[]? filterParams = null,
        IEnumerable<string>? columns = null)
    {
        var quotedTable = ValidateTable(tableName);
        if (quotedTable == null)
            throw new ArgumentException($"Table '{tableName}' is not in the metadata allowlist.", nameof(tableName));

        pageSize = ClampPageSize(pageSize);

        // Determine which columns to select
        string selectColumns;
        if (columns != null && columns.Any())
        {
            var validated = ValidateColumns(tableName, columns);
            if (validated == null)
                throw new ArgumentException($"One or more columns are not valid for table '{tableName}'.");
            selectColumns = string.Join(", ", validated);
        }
        else
        {
            // Select all active columns for the table
            var metaCols = _metadataGraph.GetColumns(tableName);
            var validColumns = metaCols.Where(c => c.IsActive).ToList();
            if (validColumns.Count == 0)
                selectColumns = "1"; // No active columns — always returns empty
            else
                selectColumns = string.Join(", ", validColumns.Select(c => $"\"{c.ColumnName}\""));
        }

        var sb = new StringBuilder();
        sb.Append("SELECT ");
        sb.Append(selectColumns);
        sb.Append(" FROM ");
        sb.Append(quotedTable);

        var allParams = new List<NpgsqlParameter>();

        // Where clause + predicates
        var whereClauses = new List<string>();

        if (!string.IsNullOrWhiteSpace(filterSql))
        {
            whereClauses.Add($"({filterSql})");
            if (filterParams != null)
                allParams.AddRange(filterParams);
        }

        // Tenant/Org predicates (Phase 4 wiring; null in Phase 3)
        if (!string.IsNullOrWhiteSpace(context.TenantPredicate))
            whereClauses.Add($"({context.TenantPredicate})");

        if (!string.IsNullOrWhiteSpace(context.OrgPredicate))
            whereClauses.Add($"({context.OrgPredicate})");

        if (whereClauses.Count > 0)
        {
            sb.Append(" WHERE ");
            sb.Append(string.Join(" AND ", whereClauses));
        }

        // Ordering
        var sort = ValidateSort(tableName, sortBy, sortDir);
        if (sort != null)
        {
            sb.Append(" ORDER BY ");
            sb.Append(sort.Value.Column);
            sb.Append(' ');
            sb.Append(sort.Value.Direction);
        }

        // Pagination
        sb.Append(" OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY");
        allParams.Add(new NpgsqlParameter("@Offset", page > 0 ? (page - 1) * pageSize : 0));
        allParams.Add(new NpgsqlParameter("@PageSize", pageSize));

        // Count query (without ORDER BY / pagination)
        var countSb = new StringBuilder();
        countSb.Append("SELECT COUNT(*) FROM ");
        countSb.Append(quotedTable);

        var countWhere = new List<string>();
        if (!string.IsNullOrWhiteSpace(filterSql))
        {
            countWhere.Add($"({filterSql})");
        }
        if (!string.IsNullOrWhiteSpace(context.TenantPredicate))
            countWhere.Add($"({context.TenantPredicate})");
        if (!string.IsNullOrWhiteSpace(context.OrgPredicate))
            countWhere.Add($"({context.OrgPredicate})");

        if (countWhere.Count > 0)
        {
            countSb.Append(" WHERE ");
            countSb.Append(string.Join(" AND ", countWhere));
        }

        return (sb.ToString(), allParams.ToArray(), countSb.ToString());
    }

    /// <summary>
    /// Builds a parameterized INSERT query.
    /// Validates all column names against metadata. Returns (sql, params).
    /// </summary>
    public (string Sql, NpgsqlParameter[] Parameters) BuildInsert(
        string tableName,
        IReadOnlyDictionary<string, object?> data,
        IReadOnlyContext context)
    {
        var quotedTable = ValidateTable(tableName);
        if (quotedTable == null)
            throw new ArgumentException($"Table '{tableName}' is not in the metadata allowlist.", nameof(tableName));

        // Filter out system columns
        var columns = _metadataGraph.GetColumns(tableName);
        var writableColumns = columns.Where(c => c.IsActive && c.IsUpdateable).Select(c => c.ColumnName).ToHashSet();
        var validData = new Dictionary<string, object?>();

        foreach (var kv in data)
        {
            // Only allow writable columns
            if (writableColumns.Contains(kv.Key))
                validData[kv.Key] = kv.Value;
        }

        if (validData.Count == 0)
            throw new ArgumentException("No writable columns provided for INSERT.", nameof(data));

        var colNames = validData.Keys.ToList();
        var parameters = new List<NpgsqlParameter>();
        var columnParts = new List<string>();
        var valueParts = new List<string>();

        for (int i = 0; i < colNames.Count; i++)
        {
            var col = colNames[i];
            var quoted = ValidateColumn(tableName, col);
            if (quoted == null)
                continue;

            var paramName = $"@p{i}";
            var value = validData[col];
            parameters.Add(new NpgsqlParameter(paramName, value ?? DBNull.Value));

            columnParts.Add(quoted);
            valueParts.Add(paramName);
        }

        var sb = new StringBuilder();
        sb.Append("INSERT INTO ");
        sb.Append(quotedTable);
        sb.Append(" (");
        sb.Append(string.Join(", ", columnParts));
        sb.Append(") VALUES (");
        sb.Append(string.Join(", ", valueParts));
        sb.Append(")");

        return (sb.ToString(), parameters.ToArray());
    }

    /// <summary>
    /// Builds a parameterized UPDATE query.
    /// Returns (sql, params).
    /// </summary>
    public (string Sql, NpgsqlParameter[] Parameters) BuildUpdate(
        string tableName,
        string idColumnName,
        IReadOnlyDictionary<string, object?> data,
        IReadOnlyContext context)
    {
        var quotedTable = ValidateTable(tableName);
        if (quotedTable == null)
            throw new ArgumentException($"Table '{tableName}' is not in the metadata allowlist.", nameof(tableName));

        var quotedId = ValidateColumn(tableName, idColumnName);
        if (quotedId == null)
            throw new ArgumentException($"ID column '{idColumnName}' is not valid for table '{tableName}'.");

        // Filter to writable columns only
        var columns = _metadataGraph.GetColumns(tableName);
        var writableColumns = columns.Where(c => c.IsActive && c.IsUpdateable).Select(c => c.ColumnName).ToHashSet();
        var validData = new Dictionary<string, object?>();

        foreach (var kv in data)
        {
            if (writableColumns.Contains(kv.Key))
                validData[kv.Key] = kv.Value;
        }

        if (validData.Count == 0)
            throw new ArgumentException("No writable columns provided for UPDATE.", nameof(data));

        var parameters = new List<NpgsqlParameter>();
        var setParts = new List<string>();

        for (int i = 0; i < validData.Count; i++)
        {
            var kv = validData.ElementAt(i);
            var quoted = ValidateColumn(tableName, kv.Key);
            if (quoted == null)
                continue;

            var paramName = $"@p{i}";
            parameters.Add(new NpgsqlParameter(paramName, kv.Value ?? DBNull.Value));
            setParts.Add($"{quoted} = {paramName}");
        }

        var sb = new StringBuilder();
        sb.Append("UPDATE ");
        sb.Append(quotedTable);
        sb.Append(" SET ");
        sb.Append(string.Join(", ", setParts));
        sb.Append($" WHERE {quotedId} = @Id");
        parameters.Add(new NpgsqlParameter("@Id", DBNull.Value)); // placeholder — set actual id before execution

        return (sb.ToString(), parameters.ToArray());
    }

    /// <summary>
    /// Builds a parameterized DELETE query.
    /// Includes tenant/org predicates from context.
    /// Returns (sql, params).
    /// </summary>
    public (string Sql, NpgsqlParameter[] Parameters) BuildDelete(
        string tableName,
        string idColumnName,
        IReadOnlyContext context)
    {
        var quotedTable = ValidateTable(tableName);
        if (quotedTable == null)
            throw new ArgumentException($"Table '{tableName}' is not in the metadata allowlist.", nameof(tableName));

        var quotedId = ValidateColumn(tableName, idColumnName);
        if (quotedId == null)
            throw new ArgumentException($"ID column '{idColumnName}' is not valid for table '{tableName}'.");

        var sb = new StringBuilder();
        sb.Append("DELETE FROM ");
        sb.Append(quotedTable);
        sb.Append(" WHERE ");
        sb.Append(quotedId);
        sb.Append(" = @Id");

        var predicates = new List<string>();
        if (!string.IsNullOrWhiteSpace(context.TenantPredicate))
            predicates.Add(context.TenantPredicate);
        if (!string.IsNullOrWhiteSpace(context.OrgPredicate))
            predicates.Add(context.OrgPredicate);

        foreach (var pred in predicates)
            sb.Append($" AND {pred}");

        return (sb.ToString(), Array.Empty<NpgsqlParameter>());
    }

    /// <summary>
    /// Validates a reference table name for lookup operations.
    /// Returns quoted identifier or null.
    /// </summary>
    public string? ValidateReferenceTable(string tableName)
    {
        return ValidateTable(tableName);
    }

    private static int ClampPageSize(int pageSize)
    {
        if (pageSize < MinPageSize)
            throw new ArgumentOutOfRangeException(nameof(pageSize), $"pageSize must be >= {MinPageSize}.");
        if (pageSize > MaxPageSize)
            throw new ArgumentOutOfRangeException(nameof(pageSize), $"pageSize must be <= {MaxPageSize}.");
        return pageSize;
    }
}
