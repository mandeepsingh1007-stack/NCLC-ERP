using Npgsql;
using NpgsqlTypes;
using System.Linq;
using System.Text.Json;

namespace Platform.Core.Runtime;

/// <summary>
/// Parses and validates filter DSL from ADR-0007.
/// Converts JSON filter AST → validated AST → parameterized SQL WHERE clause.
/// NEVER generates raw SQL concatenation for values.
/// </summary>
public class FilterParser
{
    private const int MaxNestingDepth = 10;
    private const int MaxClauses = 50;
    private const int MaxFilterLength = 4096;

    // 13 allowed operators (ADR-0007)
    private static readonly HashSet<string> AllowedOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "eq", "ne", "gt", "gte", "lt", "lte",
        "like", "ilike", "in", "not in", "between", "notnull", "null"
    };

    // Operators that require no value
    private static readonly HashSet<string> NoValueOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "notnull", "null"
    };

    // Operators that take an array of values
    private static readonly HashSet<string> ArrayOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "in", "not in", "between"
    };

    /// <summary>
    /// Parses and validates a JSON filter string.
    /// The tableColumnNames is used for column allowlist validation.
    /// </summary>
    /// <param name="filterJson">JSON filter AST (URL-encoded in query param)</param>
    /// <param name="tableColumnNames">Allowed column names for the table (from SysColumn metadata)</param>
    /// <returns>Validated filter with parameterized WHERE clause and NpgsqlParameters</returns>
    public ValidatedFilter Parse(string? filterJson, IEnumerable<string> tableColumnNames)
    {
        if (string.IsNullOrWhiteSpace(filterJson))
            return new ValidatedFilter("", Array.Empty<NpgsqlParameter>(), 0);

        if (filterJson.Length > MaxFilterLength)
            throw new ArgumentException($"Filter exceeds maximum length of {MaxFilterLength} characters", nameof(filterJson));

        var columnAllowlist = new HashSet<string>(tableColumnNames, StringComparer.OrdinalIgnoreCase);

        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        using var doc = JsonDocument.Parse(filterJson, new JsonDocumentOptions { AllowTrailingCommas = false });

        int clauseCount = 0;
        var sqlParts = new List<string>();
        var parameters = new List<NpgsqlParameter>();

        var root = doc.RootElement;
        var parsed = ParseNode(root, columnAllowlist, 0, 1, ref clauseCount);

        if (clauseCount > MaxClauses)
            throw new ArgumentException($"Filter exceeds maximum of {MaxClauses} clauses", nameof(filterJson));

        if (parsed != null)
        {
            var sql = BuildSql(parsed, parameters);
            if (!string.IsNullOrEmpty(sql))
                sqlParts.Add(sql);
        }

        var whereClause = sqlParts.Count > 0 ? "WHERE " + string.Join(" AND ", sqlParts) : "";
        return new ValidatedFilter(whereClause, parameters.ToArray(), clauseCount);
    }

    /// <summary>
    /// Parse a single clause: { "column": "Status", "op": "eq", "value": "Active" }
    /// </summary>
    private FilterAstNode? ParseClause(JsonElement element, HashSet<string> columnAllowlist, int depth, int maxDepth, ref int clauseCount)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        string? column = null;
        string? @operator = null;
        object? value = null;
        object?[]? values = null;

        foreach (var prop in element.EnumerateObject())
        {
            if (prop.NameEquals("column"))
            {
                var rawColumn = prop.Value.GetString();
                if (string.IsNullOrWhiteSpace(rawColumn))
                    return null;

                if (!columnAllowlist.Contains(rawColumn))
                    throw new ArgumentException($"Unknown column: {rawColumn}");

                // Resolve to canonical name from allowlist for consistent SQL output
                column = columnAllowlist.First(c => string.Equals(c, rawColumn, StringComparison.OrdinalIgnoreCase));
            }
            else if (prop.NameEquals("op"))
            {
                @operator = prop.Value.GetString();
                if (string.IsNullOrWhiteSpace(@operator) || !AllowedOperators.Contains(@operator!))
                    throw new ArgumentException($"Unknown operator: {@operator}");
            }
            else if (prop.NameEquals("value"))
            {
                if (NoValueOperators.Contains(@operator ?? ""))
                    return null; // notnull/null should not have value

                value = ExtractValue(prop.Value);
            }
            else if (prop.NameEquals("values"))
            {
                if (!ArrayOperators.Contains(@operator ?? ""))
                    return null; // in/between should use "values"

                values = ExtractValuesArray(prop.Value);
            }
        }

        if (string.IsNullOrWhiteSpace(column) || string.IsNullOrWhiteSpace(@operator))
            return null;

        if (!NoValueOperators.Contains(@operator) && value == null && values == null)
            return null;

        // Array operators: 'in'/'not in' count each value as a clause, 'between' counts as 1
        if (values != null && (@operator == "in" || @operator == "not in"))
            clauseCount += values.Length;
        else
            clauseCount++;
        return FilterAstNode.Clause(column!, @operator!, value, values);
    }

    /// <summary>
    /// Parse a boolean node: { "type": "boolean", "op": "$and", "clauses": [...] }
    /// Or compact notation: { "Status": { "op": "eq", "value": "Active" }, "Type": { "op": "ne", "value": "Deleted" } }
    /// Compact notation implicitly ANDs across keys.
    /// </summary>
    private FilterAstNode? ParseBooleanNode(JsonElement element, HashSet<string> columnAllowlist, int depth, int maxDepth, ref int clauseCount)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        // Check for explicit boolean wrapper
        if (element.TryGetProperty("type", out var typeProp) &&
            typeProp.ValueKind == JsonValueKind.String &&
            typeProp.GetString() == "boolean")
        {
            var opProp = element.GetProperty("op");
            var op = opProp.GetString();

            if (op != "$and" && op != "$or" && op != "$not")
                throw new ArgumentException($"Invalid boolean operator: {op}");

            if (depth > maxDepth)
                throw new ArgumentException($"Filter nesting depth exceeds maximum of {maxDepth} levels");

            var clausesProp = element.GetProperty("clauses");
            if (clausesProp.ValueKind != JsonValueKind.Array)
                return null;

            var childClauses = new List<FilterAstNode>();
            foreach (var clauseElem in clausesProp.EnumerateArray())
            {
                var child = ParseNode(clauseElem, columnAllowlist, depth + 1, maxDepth, ref clauseCount);
                if (child != null)
                    childClauses.Add(child);
            }

            if (childClauses.Count == 0)
                return null;

            return FilterAstNode.Boolean(op, childClauses.ToArray());
        }

        // Compact notation: top-level object without "type" → implicit AND across keys
        if (depth <= 1 && !element.EnumerateObject().Any(p => p.NameEquals("type")))
        {
            var childClauses = new List<FilterAstNode>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("type") || prop.NameEquals("op") || prop.NameEquals("clauses"))
                    continue;

                if (prop.Value.ValueKind == JsonValueKind.Object)
                {
                    if (!prop.Value.TryGetProperty("op", out var opProp) || opProp.ValueKind != JsonValueKind.String)
                        continue;

                    if (!AllowedOperators.Contains(opProp.GetString() ?? ""))
                        continue;

                    // This is a column shorthand: { "Status": { "op": "eq", "value": "Active" } }
                    var column = prop.Name;
                    if (!columnAllowlist.Contains(column))
                        throw new ArgumentException($"Unknown column: {column}");

                    var @operator = opProp.GetString()!;
                    object? val = null;
                    object?[]? vals = null;

                    prop.Value.TryGetProperty("value", out var valueProp);
                    prop.Value.TryGetProperty("values", out var valuesProp);

                    if (NoValueOperators.Contains(@operator))
                    {
                        // no value needed
                    }
                    else if (ArrayOperators.Contains(@operator))
                    {
                        vals = valuesProp.ValueKind == JsonValueKind.Array ? ExtractValuesArray(valuesProp) : null;
                    }
                    else
                    {
                        val = valueProp.ValueKind != JsonValueKind.Undefined ? ExtractValue(valueProp) : null;
                    }

                    clauseCount++;
                    childClauses.Add(FilterAstNode.Clause(column, @operator, val, vals));
                }
            }

            if (childClauses.Count == 0)
                return null;

            return FilterAstNode.Boolean("$and", childClauses.ToArray());
        }

        return null;
    }

    /// <summary>
    /// Dispatch to clause or boolean parser based on AST structure.
    /// </summary>
    private FilterAstNode? ParseNode(JsonElement element, HashSet<string> columnAllowlist, int depth, int maxDepth, ref int clauseCount)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        bool hasColumn = element.TryGetProperty("column", out var colProp) && colProp.ValueKind == JsonValueKind.String;
        bool hasOp = element.TryGetProperty("op", out var opProp) && opProp.ValueKind == JsonValueKind.String;
        bool hasValue = element.TryGetProperty("value", out var valProp);
        bool hasValues = element.TryGetProperty("values", out var valsProp);

        // Look for "type" property to distinguish boolean wrapper from clause
        bool isBooleanWrapper = element.TryGetProperty("type", out var typeProp) &&
            typeProp.ValueKind == JsonValueKind.String &&
            typeProp.GetString() == "boolean";

        // If it has "value" or "values" but no "column", it's a malformed clause
        // (unless it's a boolean wrapper with these properties)
        if ((hasValue || hasValues) && !hasColumn && !isBooleanWrapper)
            throw new ArgumentException("Filter clause is missing required 'column' property");

        // Check if it has "column" and "op" → clause
        if (hasColumn && hasOp)
        {
            return ParseClause(element, columnAllowlist, depth, maxDepth, ref clauseCount);
        }

        // Otherwise → boolean node
        return ParseBooleanNode(element, columnAllowlist, depth, maxDepth, ref clauseCount);
    }

    private static object? ExtractValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => null
        };
    }

    private static object?[]? ExtractValuesArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
            return null;

        var result = new object?[element.GetArrayLength()];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = ExtractValue(element[i]);
        }
        return result;
    }

    /// <summary>
    /// Recursively builds a parameterized SQL WHERE fragment from the validated AST.
    /// </summary>
    private static string BuildSql(FilterAstNode node, List<NpgsqlParameter> parameters)
    {
        if (node.Type == FilterNodeType.Clause)
            return BuildClauseSql(node, parameters);

        // Boolean node ($and, $or, $not)
        var parts = new List<string>();
        foreach (var child in node.Clauses!)
            parts.Add(BuildSql(child, parameters));

        if (parts.Count == 0)
            return "";

        if (node.Operator == "$not" && parts.Count == 1)
            return $"NOT ({parts[0]})";

        var joinOp = node.Operator == "$or" ? " OR " : " AND ";
        var inner = string.Join(joinOp, parts);

        if (node.Operator == "$not")
            return $"NOT ({inner})";

        return $"({inner})";
    }

    private static string BuildClauseSql(FilterAstNode node, List<NpgsqlParameter> parameters)
    {
        var paramName = $"p{parameters.Count}";
        var column = node.Column!;
        var @operator = node.Operator!;

        if (NoValueOperators.Contains(@operator))
        {
            return @operator == "null" ? $"{column} IS NULL" : $"{column} IS NOT NULL";
        }

        if (@operator == "in" || @operator == "not in")
        {
            var vals = node.Values!;
            if (vals.Length == 0)
                return @operator == "in" ? "1=0" : "1=1";

            var paramNames = new List<string>();
            foreach (var v in vals)
            {
                if (v == null) continue;
                var name = $"p{parameters.Count}";
                parameters.Add(CreateParameter(name, v));
                paramNames.Add(name);
            }

            if (paramNames.Count == 0)
                return @operator == "in" ? "1=0" : "1=1";

            var list = string.Join(", ", paramNames);
            return @operator == "in" ? $"{column} IN ({list})" : $"{column} NOT IN ({list})";
        }

        if (@operator == "between")
        {
            var vals = node.Values!;
            if (vals.Length != 2)
                return "1=0";

            var name1 = $"p{parameters.Count}";
            var name2 = $"p{parameters.Count + 1}";
            parameters.Add(CreateParameter(name1, vals[0]!));
            parameters.Add(CreateParameter(name2, vals[1]!));

            return $"{column} BETWEEN {name1} AND {name2}";
        }

        // Single-value operators
        if (node.Value == null)
            return "1=0"; // Can't compare against null → always false

        var paramNameSingle = $"p{parameters.Count}";
        parameters.Add(CreateParameter(paramNameSingle, node.Value!));

        return @operator switch
        {
            "eq" => $"{column} = {paramNameSingle}",
            "ne" => $"{column} != {paramNameSingle}",
            "gt" => $"{column} > {paramNameSingle}",
            "gte" => $"{column} >= {paramNameSingle}",
            "lt" => $"{column} < {paramNameSingle}",
            "lte" => $"{column} <= {paramNameSingle}",
            "like" => $"{column} LIKE {paramNameSingle}",
            "ilike" => $"{column} ILIKE {paramNameSingle}",
            _ => "1=0"
        };
    }

    private static NpgsqlParameter CreateParameter(string name, object value)
    {
        var param = new NpgsqlParameter { ParameterName = name, Value = value is null ? DBNull.Value : value };
        param.NpgsqlDbType = InferType(value!);
        return param;
    }

    private static NpgsqlDbType InferType(object value)
    {
        if (value is string) return NpgsqlDbType.Text;
        if (value is int) return NpgsqlDbType.Integer;
        if (value is long) return NpgsqlDbType.Bigint;
        if (value is float) return NpgsqlDbType.Double;
        if (value is double) return NpgsqlDbType.Double;
        if (value is decimal) return NpgsqlDbType.Numeric;
        if (value is bool) return NpgsqlDbType.Boolean;
        if (value is DateTime) return NpgsqlDbType.Date;
        return NpgsqlDbType.Text;
    }
}
