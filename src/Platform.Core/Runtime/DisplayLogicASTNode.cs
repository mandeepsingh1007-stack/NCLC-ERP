using Platform.Core.Metadata;

namespace Platform.Core.Runtime;

/// <summary>
/// AST node types for display logic (ADR-0006).
/// </summary>
internal abstract class DisplayLogicASTNode
{
    public abstract bool Evaluate(IReadOnlyContext context, IDictionary<string, object?> poValues);
}

internal class AndNode : DisplayLogicASTNode
{
    public DisplayLogicASTNode Left { get; }
    public DisplayLogicASTNode Right { get; }

    public AndNode(DisplayLogicASTNode left, DisplayLogicASTNode right)
    {
        Left = left;
        Right = right;
    }

    public override bool Evaluate(IReadOnlyContext context, IDictionary<string, object?> poValues)
    {
        var left = Left.Evaluate(context, poValues);
        if (!left) return false;
        return Right.Evaluate(context, poValues);
    }
}

internal class OrNode : DisplayLogicASTNode
{
    public DisplayLogicASTNode Left { get; }
    public DisplayLogicASTNode Right { get; }

    public OrNode(DisplayLogicASTNode left, DisplayLogicASTNode right)
    {
        Left = left;
        Right = right;
    }

    public override bool Evaluate(IReadOnlyContext context, IDictionary<string, object?> poValues)
    {
        var left = Left.Evaluate(context, poValues);
        if (left) return true;
        return Right.Evaluate(context, poValues);
    }
}

internal class NotNode : DisplayLogicASTNode
{
    public DisplayLogicASTNode Operand { get; }

    public NotNode(DisplayLogicASTNode operand)
    {
        Operand = operand;
    }

    public override bool Evaluate(IReadOnlyContext context, IDictionary<string, object?> poValues)
    {
        return !Operand.Evaluate(context, poValues);
    }
}

internal class FieldRefNode : DisplayLogicASTNode
{
    public string FieldName { get; }

    public FieldRefNode(string fieldName)
    {
        FieldName = fieldName;
    }

    public override bool Evaluate(IReadOnlyContext context, IDictionary<string, object?> poValues)
    {
        var value = ResolveInstance(poValues);
        return value != null && value != DBNull.Value;
    }

    public static object? Resolve(IReadOnlyContext context, IDictionary<string, object?> poValues)
    {
        // Stub — actual field name resolution requires context from the parser.
        // When called via Evaluate(), the instance FieldName is set but this static
        // method has no access to it. Callers should use the instance ResolveValue()
        // which delegates to an instance-aware method.
        return null;
    }

    public object? ResolveInstance(IDictionary<string, object?> poValues)
    {
        poValues.TryGetValue(FieldName, out var value);
        return value;
    }

    public object? ResolveValue(IReadOnlyContext context, IDictionary<string, object?> poValues)
    {
        return ResolveInstance(poValues);
    }
}

internal class LiteralNode : DisplayLogicASTNode
{
    public object? Value { get; }

    public LiteralNode(object? value)
    {
        Value = value;
    }

    public override bool Evaluate(IReadOnlyContext context, IDictionary<string, object?> poValues)
    {
        return Value switch
        {
            bool b => b,
            null => false,
            _ => Convert.ToBoolean(Value)
        };
    }
}

internal class ComparisonNode : DisplayLogicASTNode
{
    public string Operator { get; }
    public DisplayLogicASTNode Left { get; }
    public DisplayLogicASTNode Right { get; }

    public ComparisonNode(string @operator, DisplayLogicASTNode left, DisplayLogicASTNode right)
    {
        Operator = @operator;
        Left = left;
        Right = right;
    }

    public override bool Evaluate(IReadOnlyContext context, IDictionary<string, object?> poValues)
    {
        object? left = GetRawValue(Left, context, poValues);
        object? right = GetRawValue(Right, context, poValues);
        return Compare(left, right, Operator);
    }

    private static object? GetRawValue(DisplayLogicASTNode node, IReadOnlyContext context, IDictionary<string, object?> poValues)
    {
        return node switch
        {
            FieldRefNode fieldRef => fieldRef.ResolveInstance(poValues),
            LiteralNode literal => literal.Value,
            _ => node.Evaluate(context, poValues)
        };
    }

    private static bool Compare(object? left, object? right, string op)
    {
        var leftType = left?.GetType();
        var rightType = right?.GetType();

        if (left != null && right != null && leftType != rightType)
        {
            return false;
        }

        if (left == null && right == null)
            return op == "==" || op == "eq";
        if (left == null || right == null)
            return false;

        return op switch
        {
            "==" or "eq" => Equals(left, right),
            "!=" or "ne" or "<>" => !Equals(left, right),
            "<" => CompareValues(left, right) < 0,
            ">" => CompareValues(left, right) > 0,
            "<=" => CompareValues(left, right) <= 0,
            ">=" => CompareValues(left, right) >= 0,
            "like" => LikeMatch(left.ToString()!, right?.ToString()!),
            _ => false
        };
    }

    private static int CompareValues(object? left, object? right)
    {
        return (left, right) switch
        {
            (int l, int r) => l.CompareTo(r),
            (long l, long r) => l.CompareTo(r),
            (double l, double r) => l.CompareTo(r),
            (DateTime l, DateTime r) => l.CompareTo(r),
            (string l, string r) => string.Compare(l, r, StringComparison.Ordinal),
            _ => 0
        };
    }

    private static bool LikeMatch(string input, string? pattern)
    {
        if (pattern == null) return false;

        // Convert SQL LIKE pattern to regex: % → *, _ → ., then escape remaining regex chars
        var regexPattern = pattern.Replace("%", "*").Replace("_", ".");
        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(regexPattern)
            .Replace(@"\*", ".*")
            .Replace(@"\.", ".") + "$";

        return System.Text.RegularExpressions.Regex.IsMatch(input, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
