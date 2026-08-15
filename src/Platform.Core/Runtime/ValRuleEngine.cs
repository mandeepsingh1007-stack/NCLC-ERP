using System.Data;
using System.Text.RegularExpressions;

using Platform.Core.Metadata;

namespace Platform.Core.Runtime;

/// <summary>
/// Secure ValRule evaluation engine.
///
/// Phase 2 supported rule types:
/// - SQL: SELECT-only, parameterized @Value, table whitelist, function whitelist
/// - REGEX: default options only, 100ms timeout, no flags
///
/// NOT supported in Phase 2 (deferred):
/// - LAMBDA: pre-registered delegates only
/// - SCRIPT: entirely deferred
///
/// Security constraints:
/// - Never concatenate user values into SQL
/// - Only approved execution mechanisms
/// - Reject non-SELECT SQL
/// - Timeout all operations
/// </summary>
public class ValRuleEngine : IValRuleEngine
{
    private static readonly HashSet<string> AllowedSqlFunctions = new(System.StringComparer.OrdinalIgnoreCase)
    {
        "COUNT", "SUM", "AVG", "MAX", "MIN", "EXISTS", "CASE",
        "COALESCE", "NULLIF", "ROW_NUMBER", "RANK", "DENSE_RANK",
        "UPPER", "LOWER", "TRIM", "LENGTH", "SUBSTRING", "CAST"
    };

    private const int SqlTimeoutMs = 100;
    private const int RegexTimeoutMs = 100;

    private readonly string _connectionString;
    private readonly HashSet<string> _allowedTables;

    public ValRuleEngine(string connectionString, IEnumerable<string> allowedTables)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A database connection string is required.", nameof(connectionString));
        }
        _connectionString = connectionString;
        _allowedTables = new HashSet<string>(allowedTables, System.StringComparer.OrdinalIgnoreCase);
    }

    public ValRuleResult Evaluate(SysValRule rule, object? value, IReadOnlyContext context)
    {
        if (rule == null)
        {
            return ValRuleResult.Fail("Null rule", "Validation rule is null.");
        }

        if (!rule.IsActive)
        {
            return ValRuleResult.Pass(rule.Name);
        }

        return rule.RuleType switch
        {
            Metadata.ValRuleTypeEnum.Sql => EvaluateSql(rule, value, context),
            Metadata.ValRuleTypeEnum.Regex => EvaluateRegex(rule, value),
            Metadata.ValRuleTypeEnum.Lambda => ValRuleResult.Fail(
                rule.Name, "Lambda rules are not supported in Phase 2."),
            Metadata.ValRuleTypeEnum.Script => ValRuleResult.Fail(
                rule.Name, "Script rules are not supported in Phase 2."),
            _ => ValRuleResult.Fail(rule.Name, $"Unknown rule type: {rule.RuleType}")
        };
    }

    public IReadOnlyList<ValRuleResult> EvaluateBatch(string tableName, object? value, IReadOnlyContext context)
    {
        // Batch evaluation for all ValRules on a table
        // In Phase 2, batch resolution is deferred — this returns an empty list
        return Array.Empty<ValRuleResult>();
    }

    private ValRuleResult EvaluateSql(SysValRule rule, object? value, IReadOnlyContext context)
    {
        var sql = rule.Code;

        if (string.IsNullOrEmpty(sql))
        {
            return ValRuleResult.Fail(rule.Name, "ValRule code is empty.");
        }

        // Security check 1: Must be SELECT-only
        if (!IsSelectStatement(sql))
        {
            return ValRuleResult.Fail(rule.Name, "SQL ValRule must be a SELECT statement only.");
        }

        // Security check 2: Check for disallowed keywords and system catalog access
        if (ContainsDisallowedSqlKeywords(sql))
        {
            return ValRuleResult.Fail(rule.Name, "SQL ValRule contains disallowed keywords or constructs.");
        }

        // Security check 2a: Block access to system catalogs (pg_catalog, information_schema, etc.)
        if (ContainsSystemCatalogAccess(sql))
        {
            return ValRuleResult.Fail(rule.Name, "SQL ValRule cannot access system catalog tables.");
        }

        // Security check 2b: Table allowlist — prevent access to unauthorized tables
        if (_allowedTables.Count > 0 && ContainsUnauthorizedTable(sql, _allowedTables))
        {
            return ValRuleResult.Fail(rule.Name, "SQL ValRule references a table not in the allowed list.");
        }

        // Security check 3: Tenant/org isolation — inject predicates if present
        var tenantPredicate = context.TenantPredicate;
        var orgPredicate = context.OrgPredicate;

        // If TenantId is set but no predicate is provided, fail safely
        if (!string.IsNullOrEmpty(context.TenantId) && string.IsNullOrEmpty(tenantPredicate))
        {
            return ValRuleResult.Fail(rule.Name, "Tenant isolation predicate not configured. Cannot execute SQL without tenant predicate.");
        }
        if (!string.IsNullOrEmpty(context.OrgId) && string.IsNullOrEmpty(orgPredicate))
        {
            return ValRuleResult.Fail(rule.Name, "Org isolation predicate not configured. Cannot execute SQL without org predicate.");
        }

        // Build final SQL with predicates
        string finalSql = sql;
        var predicates = new List<string>();
        if (!string.IsNullOrEmpty(tenantPredicate))
            predicates.Add(tenantPredicate);
        if (!string.IsNullOrEmpty(orgPredicate))
            predicates.Add(orgPredicate);

        if (predicates.Count > 0)
        {
            // Wrap in subquery to safely combine with arbitrary SELECT
            finalSql = $"SELECT * FROM ({sql}) AS _valrule_query WHERE {string.Join(" AND ", predicates)}";
        }

        // Security check 4: Execute parameterized query
        try
        {
            using var conn = new Npgsql.NpgsqlConnection(_connectionString);
            conn.Open();

            using var cmd = new Npgsql.NpgsqlCommand(finalSql, conn);
            cmd.CommandTimeout = Math.Max(1, SqlTimeoutMs / 1000);

            // Always parameterize the value
            cmd.Parameters.AddWithValue("@Value", value ?? DBNull.Value);

            // Add tenant/org parameters for predicate injection
            if (!string.IsNullOrEmpty(context.TenantId))
                cmd.Parameters.AddWithValue("@TenantId", context.TenantId);
            if (!string.IsNullOrEmpty(context.OrgId))
                cmd.Parameters.AddWithValue("@OrgId", context.OrgId);

            var result = cmd.ExecuteScalar();
            return ConvertSqlResult(result, rule.Name);
        }
        catch (TimeoutException)
        {
            return ValRuleResult.Fail(rule.Name, $"SQL ValRule timed out after {SqlTimeoutMs}ms.");
        }
        catch (Npgsql.PostgresException)
        {
            // Log and fail safely — don't leak DB errors
            return ValRuleResult.Fail(rule.Name, "SQL ValRule execution failed.");
        }
        catch
        {
            return ValRuleResult.Fail(rule.Name, "SQL ValRule execution failed.");
        }
    }

    private ValRuleResult EvaluateRegex(SysValRule rule, object? value)
    {
        if (value == null || string.IsNullOrEmpty(rule.Code))
        {
            return ValRuleResult.Pass(rule.Name);
        }

        var valueStr = value.ToString() ?? string.Empty;
        var pattern = rule.Code;

        try
        {
            // No options, no flags — default matching only
            var timeout = new System.Threading.CancellationTokenSource();
            timeout.CancelAfter(RegexTimeoutMs);

            bool matched;
            try
            {
                matched = Regex.IsMatch(valueStr, pattern, RegexOptions.None, TimeSpan.FromMilliseconds(RegexTimeoutMs));
            }
            catch (OperationCanceledException)
            {
                return ValRuleResult.Fail(rule.Name, $"Regex evaluation timed out after {RegexTimeoutMs}ms.");
            }

            return matched
                ? ValRuleResult.Pass(rule.Name)
                : ValRuleResult.Fail(rule.Name, $"Value '{valueStr}' does not match pattern '{pattern}'.");
        }
        catch (ArgumentException ex)
        {
            return ValRuleResult.Fail(rule.Name, $"Invalid regex pattern: {ex.Message}");
        }
    }

    private static bool IsSelectStatement(string sql)
    {
        var trimmed = sql.Trim();
        // Strip comments
        trimmed = System.Text.RegularExpressions.Regex.Replace(trimmed, @"--.*$", "", System.Text.RegularExpressions.RegexOptions.Multiline);
        trimmed = System.Text.RegularExpressions.Regex.Replace(trimmed, @"/\*.*?\*/", "", System.Text.RegularExpressions.RegexOptions.Compiled);
        trimmed = trimmed.TrimStart();

        // Must start with SELECT — nothing else allowed
        if (!trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Check for CTE (WITH ... AS) at the start — reject, CTEs can mask non-SELECT statements
        if (trimmed.StartsWith("WITH ", StringComparison.OrdinalIgnoreCase))
        {
            // Actually, CTEs always start with SELECT, but we need to check for
            // CTE-bypass patterns like `WITH (AS ...` disguised as other things
            // Per SEC-P2-001: no CTEs allowed in Phase 2
            return false;
        }

        return true;
    }

    private static bool ContainsSystemCatalogAccess(string sql)
    {
        var normalized = System.Text.RegularExpressions.Regex.Replace(sql, @"\s+", " ").Trim().ToUpperInvariant();

        var blockedPatterns = new[]
        {
            "PG_CATALOG.", "INFORMATION_SCHEMA.",
            "SYS.", "DBO.", "SYSADMIN.",
            "EXEC ", "EXECUTE",
            "XP_", "SP_"
        };

        foreach (var pattern in blockedPatterns)
        {
            if (normalized.Contains(pattern))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Extracts table names from FROM and JOIN clauses and checks against allowlist.
    /// Handles: FROM table, FROM table alias, JOIN table, JOIN table alias, (SELECT) subqueries.
    /// </summary>
    private static bool ContainsUnauthorizedTable(string sql, HashSet<string> allowedTables)
    {
        var normalized = sql.Trim();

        // Extract table names from FROM clause (handle aliases: FROM Users u, FROM Users AS u)
        var fromMatches = System.Text.RegularExpressions.Regex.Matches(normalized, @"\bFROM\s+(\w+)(?:\s+(?:AS\s+)?\w+)?\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        foreach (System.Text.RegularExpressions.Match m in fromMatches)
        {
            var tableName = m.Groups[1].Value.ToUpperInvariant();
            if (IsSqlKeywordOrFunction(tableName))
                continue;
            if (!allowedTables.Contains(tableName))
                return true;
        }

        // Extract table names from JOIN clauses
        var joinMatches = System.Text.RegularExpressions.Regex.Matches(normalized, @"\bJOIN\s+(\w+)(?:\s+(?:AS\s+)?\w+)?\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        foreach (System.Text.RegularExpressions.Match m in joinMatches)
        {
            var tableName = m.Groups[1].Value.ToUpperInvariant();
            if (IsSqlKeywordOrFunction(tableName))
                continue;
            if (!allowedTables.Contains(tableName))
                return true;
        }

        return false;
    }

    private static bool IsSqlKeywordOrFunction(string name)
    {
        // Skip SQL keywords
        if (SqlKeywords.Contains(name))
            return true;
        // Skip function calls (already validated by function whitelist)
        if (AllowedSqlFunctions.Contains(name))
            return true;
        return false;
    }

    // SQL keywords that look like function calls but are syntax elements
    private static readonly HashSet<string> SqlKeywords = new(System.StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "FROM", "WHERE", "AND", "OR", "NOT", "IN", "LIKE", "IS", "NULL",
        "ASC", "DESC", "ORDER", "BY", "GROUP", "HAVING", "LIMIT", "OFFSET",
        "AS", "ON", "JOIN", "LEFT", "RIGHT", "INNER", "OUTER", "CROSS",
        "UNION", "INTERSECT", "EXCEPT", "ALL", "DISTINCT", "TOP", "FETCH",
        "NEXT", "ROWS", "ONLY", "OVER", "PARTITION", "WINDOW", "LATERAL",
        "CUBE", "ROLLUP", "FIRST", "LAST", "VALUE"
    };

    private static bool ContainsDisallowedSqlKeywords(string sql)
    {
        // Normalize whitespace
        var normalized = System.Text.RegularExpressions.Regex.Replace(sql, @"\s+", " ").Trim().ToUpperInvariant();

        var disallowed = new[]
        {
            "INSERT INTO", "UPDATE ", "DELETE FROM", "DROP ", "ALTER ", "CREATE ",
            "EXEC", "EXECUTE", "TRUNCATE", "MERGE INTO", "EXPLAIN PLAN"
        };

        foreach (var keyword in disallowed)
        {
            if (normalized.Contains(keyword))
            {
                return true;
            }
        }

        // Check for CTE in normalized form
        if (System.Text.RegularExpressions.Regex.IsMatch(normalized, @"^\s*WITH\b.*\bAS\s", System.Text.RegularExpressions.RegexOptions.Compiled))
        {
            return true;
        }

        // Check for function whitelist violations
        var funcPattern = @"\b([A-Z_][A-Z0-9_]*)\s*\(";
        var matches = System.Text.RegularExpressions.Regex.Matches(normalized, funcPattern);
        foreach (Match m in matches)
        {
            var funcName = m.Groups[1].Value;
            // Skip SQL keywords that match the function pattern
            if (SqlKeywords.Contains(funcName))
                continue;
            if (!AllowedSqlFunctions.Contains(funcName))
            {
                return true;
            }
        }

        return false;
    }

    private static ValRuleResult ConvertSqlResult(object? result, string ruleName)
    {
        if (result == null || result == DBNull.Value)
        {
            return ValRuleResult.Fail(ruleName, "SQL ValRule returned null.");
        }

        // Scalar SQL rules return true/false or 0/1
        if (result is bool boolResult)
        {
            return boolResult
                ? ValRuleResult.Pass(ruleName)
                : ValRuleResult.Fail(ruleName, "SQL ValRule returned false.");
        }

        if (result is int intResult)
        {
            return intResult != 0
                ? ValRuleResult.Pass(ruleName)
                : ValRuleResult.Fail(ruleName, "SQL ValRule returned 0.");
        }

        if (result is long longResult)
        {
            return longResult != 0
                ? ValRuleResult.Pass(ruleName)
                : ValRuleResult.Fail(ruleName, "SQL ValRule returned 0.");
        }

        if (result is string strResult)
        {
            return !string.IsNullOrEmpty(strResult)
                ? ValRuleResult.Pass(ruleName)
                : ValRuleResult.Fail(ruleName, "SQL ValRule returned empty string.");
        }

        // Default: non-null = true
        return ValRuleResult.Pass(ruleName);
    }
}
