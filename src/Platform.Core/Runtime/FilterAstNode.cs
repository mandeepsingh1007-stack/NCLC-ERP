namespace Platform.Core.Runtime;

/// <summary>
/// Represents a validated filter AST node (ADR-0007).
/// </summary>
public sealed class FilterAstNode
{
    public FilterNodeType Type { get; }
    public string? Column { get; }
    public string? Operator { get; }
    public object? Value { get; }
    public object?[]? Values { get; }
    public FilterAstNode[]? Clauses { get; }

    private FilterAstNode(FilterNodeType type, string? column, string? @operator, object? value, object?[]? values, FilterAstNode[]? clauses)
    {
        Type = type;
        Column = column;
        Operator = @operator;
        Value = value;
        Values = values;
        Clauses = clauses;
    }

    public static FilterAstNode Clause(string column, string @operator, object? value, object?[]? values)
        => new(FilterNodeType.Clause, column, @operator, value, values, null);

    public static FilterAstNode Boolean(string @operator, FilterAstNode[] clauses)
        => new(FilterNodeType.BooleanNode, null, @operator, null, null, clauses);
}

public enum FilterNodeType
{
    Clause,
    BooleanNode
}
