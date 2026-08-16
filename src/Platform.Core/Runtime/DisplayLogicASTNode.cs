using System.Collections;
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
        var key = FieldName.StartsWith("$") ? FieldName[1..] : FieldName;
        poValues.TryGetValue(key, out var value);
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

internal class ArrayNode : DisplayLogicASTNode
{
    public IReadOnlyList<DisplayLogicASTNode> Items { get; }
    public object?[]? EvaluatedValues { get; private set; }

    public ArrayNode(IReadOnlyList<DisplayLogicASTNode> items)
    {
        Items = items;
    }

    public override bool Evaluate(IReadOnlyContext context, IDictionary<string, object?> poValues)
    {
        EvaluatedValues = new object?[Items.Count];
        for (int i = 0; i < Items.Count; i++)
        {
            EvaluatedValues[i] = GetValue(Items[i], context, poValues);
        }
        return true; // Array literals are always truthy
    }

    private static object? GetValue(DisplayLogicASTNode node, IReadOnlyContext context, IDictionary<string, object?> poValues)
    {
        return node switch
        {
            FieldRefNode fr => fr.ResolveInstance(poValues),
            LiteralNode ln => ln.Value,
            _ => node.Evaluate(context, poValues)
        };
    }

    /// <summary>
    /// Returns the evaluated array values, lazily computing them if not already done.
    /// </summary>
    internal object?[] GetValues(IReadOnlyContext context, IDictionary<string, object?> poValues)
    {
        if (EvaluatedValues == null)
        {
            Evaluate(context, poValues);
        }
        return EvaluatedValues ?? Array.Empty<object?>();
    }
}

internal class EmptyCheckNode : DisplayLogicASTNode
{
    public bool CheckEmpty { get; }
    public DisplayLogicASTNode Operand { get; }

    public EmptyCheckNode(bool checkEmpty, DisplayLogicASTNode operand)
    {
        CheckEmpty = checkEmpty;
        Operand = operand;
    }

    public override bool Evaluate(IReadOnlyContext context, IDictionary<string, object?> poValues)
    {
        object? value = Operand switch
        {
            FieldRefNode fr => fr.ResolveInstance(poValues),
            LiteralNode ln => ln.Value,
            _ => Operand.Evaluate(context, poValues)
        };

        var isEmpty = value == null || value == DBNull.Value || value.ToString() == "";
        return CheckEmpty ? isEmpty : !isEmpty;
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
            ArrayNode arrayNode => arrayNode.GetValues(context, poValues),
            _ => node.Evaluate(context, poValues)
        };
    }

    private static bool Compare(object? left, object? right, string op)
    {
        // empty / not empty operators — check BEFORE null handling
        if (op == "empty")
        {
            return left == null || left == DBNull.Value || left.ToString() == "";
        }
        if (op == "not empty")
        {
            return left != null && left != DBNull.Value && left.ToString() != "";
        }

        // Allow set-membership and pattern-match operators to compare
        // across types (e.g. string left vs array right for "in").
        if (op != "in" && op != "not in" && op != "like" && op != "not like")
        {
            var leftType = left?.GetType();
            var rightType = right?.GetType();

            if (left != null && right != null && leftType != rightType)
            {
                return false;
            }
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
            "not like" => !LikeMatch(left.ToString()!, right?.ToString()!),
            "in" => InContains(left, right),
            "not in" => !InContains(left, right),
            _ => false
        };
    }

    /// <summary>
    /// Checks if left value is contained in the right collection (for 'in'/'not in' operators).
    /// </summary>
    private static bool InContains(object? left, object? right)
    {
        // Check if right is an ArrayNode with pre-evaluated values
        if (right is ArrayNode arrayNode)
        {
            var values = arrayNode.EvaluatedValues ?? Array.Empty<object?>();
            foreach (var item in values)
            {
                if (Equals(left, item))
                    return true;
            }
            return false;
        }
        if (right is IEnumerable<object> genList)
        {
            foreach (var item in genList)
            {
                if (Equals(left, item))
                    return true;
            }
            return false;
        }
        // Also check IEnumerable for non-generic collections
        if (right is IEnumerable nonGen)
        {
            foreach (var item in nonGen)
            {
                if (Equals(left, item))
                    return true;
            }
            return false;
        }
        return Equals(left, right);
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
