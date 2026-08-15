using Platform.Core.Metadata;
using Platform.Core.Runtime;

public class DisplayLogicEvaluatorTests
{
    private static IReadOnlyContext NullContext => InMemoryContext.Create(null, null, null);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Evaluate_NullOrEmptyExpression_ReturnsFalse(string? expression)
    {
        var evaluator = new DisplayLogicEvaluator();
        var result = evaluator.Evaluate(expression, new Dictionary<string, object?>(), NullContext);
        Assert.False(result);
        Assert.True(result.Evaluated);
    }

    [Fact]
    public void Evaluate_TrueLiteral_ReturnsTrue()
    {
        var evaluator = new DisplayLogicEvaluator();
        var result = evaluator.Evaluate("true", new Dictionary<string, object?>(), NullContext);
        Assert.True(result);
        Assert.True(result.Evaluated);
    }

    [Fact]
    public void Evaluate_FalseLiteral_ReturnsFalse()
    {
        var evaluator = new DisplayLogicEvaluator();
        var result = evaluator.Evaluate("false", new Dictionary<string, object?>(), NullContext);
        Assert.False(result);
        Assert.True(result.Evaluated);
    }

    [Fact]
    public void Evaluate_NotNullLiteral_ReturnsFalse()
    {
        var evaluator = new DisplayLogicEvaluator();
        var result = evaluator.Evaluate("null", new Dictionary<string, object?>(), NullContext);
        Assert.False(result);
    }

    [Fact]
    public void Evaluate_AndOperator_ReturnsTrue()
    {
        var evaluator = new DisplayLogicEvaluator();
        var result = evaluator.Evaluate("true && true", new Dictionary<string, object?>(), NullContext);
        Assert.True(result);
    }

    [Fact]
    public void Evaluate_AndOperator_ReturnsFalse()
    {
        var evaluator = new DisplayLogicEvaluator();
        var result = evaluator.Evaluate("true && false", new Dictionary<string, object?>(), NullContext);
        Assert.False(result);
    }

    [Fact]
    public void Evaluate_OrOperator_ReturnsTrue()
    {
        var evaluator = new DisplayLogicEvaluator();
        var result = evaluator.Evaluate("false || true", new Dictionary<string, object?>(), NullContext);
        Assert.True(result);
    }

    [Fact]
    public void Evaluate_OrOperator_ReturnsFalse()
    {
        var evaluator = new DisplayLogicEvaluator();
        var result = evaluator.Evaluate("false || false", new Dictionary<string, object?>(), NullContext);
        Assert.False(result);
    }

    [Fact]
    public void Evaluate_NotOperator_ReturnsTrue()
    {
        var evaluator = new DisplayLogicEvaluator();
        var result = evaluator.Evaluate("!false", new Dictionary<string, object?>(), NullContext);
        Assert.True(result);
    }

    [Fact]
    public void Evaluate_NotOperator_ReturnsFalse()
    {
        var evaluator = new DisplayLogicEvaluator();
        var result = evaluator.Evaluate("!true", new Dictionary<string, object?>(), NullContext);
        Assert.False(result);
    }

    [Fact]
    public void Evaluate_EqualityOperator_ReturnsTrue()
    {
        var evaluator = new DisplayLogicEvaluator();
        var result = evaluator.Evaluate("'Active' == 'Active'", new Dictionary<string, object?>(), NullContext);
        Assert.True(result);
    }

    [Fact]
    public void Evaluate_EqualityOperator_ReturnsFalse()
    {
        var evaluator = new DisplayLogicEvaluator();
        var result = evaluator.Evaluate("'Active' == 'Deleted'", new Dictionary<string, object?>(), NullContext);
        Assert.False(result);
    }

    [Fact]
    public void Evaluate_InequalityOperator_ReturnsTrue()
    {
        var evaluator = new DisplayLogicEvaluator();
        var result = evaluator.Evaluate("'Active' != 'Deleted'", new Dictionary<string, object?>(), NullContext);
        Assert.True(result);
    }

    [Fact]
    public void Evaluate_LessThanOperator_ReturnsTrue()
    {
        var evaluator = new DisplayLogicEvaluator();
        var result = evaluator.Evaluate("1 < 2", new Dictionary<string, object?>(), NullContext);
        Assert.True(result);
    }

    [Fact]
    public void Evaluate_GreaterThanOperator_ReturnsTrue()
    {
        var evaluator = new DisplayLogicEvaluator();
        var result = evaluator.Evaluate("2 > 1", new Dictionary<string, object?>(), NullContext);
        Assert.True(result);
    }

    [Fact]
    public void Evaluate_LessThanOrEqualOperator_ReturnsTrue()
    {
        var evaluator = new DisplayLogicEvaluator();
        var result = evaluator.Evaluate("1 <= 1", new Dictionary<string, object?>(), NullContext);
        Assert.True(result);
    }

    [Fact]
    public void Evaluate_GreaterThanOrEqualOperator_ReturnsTrue()
    {
        var evaluator = new DisplayLogicEvaluator();
        var result = evaluator.Evaluate("2 >= 2", new Dictionary<string, object?>(), NullContext);
        Assert.True(result);
    }

    [Fact]
    public void Evaluate_ComplexExpression_WithParentheses_ReturnsTrue()
    {
        var evaluator = new DisplayLogicEvaluator();
        var result = evaluator.Evaluate("(true && false) || (true && true)", new Dictionary<string, object?>(), NullContext);
        Assert.True(result);
    }

    [Fact]
    public void Evaluate_ComplexExpression_WithParentheses_ReturnsFalse()
    {
        var evaluator = new DisplayLogicEvaluator();
        var result = evaluator.Evaluate("(true && false) || (false && false)", new Dictionary<string, object?>(), NullContext);
        Assert.False(result);
    }

    [Fact]
    public void Evaluate_NotEqualsSymbol_ReturnsTrue()
    {
        var evaluator = new DisplayLogicEvaluator();
        var result = evaluator.Evaluate("'a' <> 'b'", new Dictionary<string, object?>(), NullContext);
        Assert.True(result);
    }

    [Fact]
    public void Evaluate_LikeOperator_ReturnsTrue()
    {
        var evaluator = new DisplayLogicEvaluator();
        var result = evaluator.Evaluate("'Hello World' like '%World%'", new Dictionary<string, object?>(), NullContext);
        Assert.True(result);
    }

    [Fact]
    public void Evaluate_LikeOperator_ReturnsFalse()
    {
        var evaluator = new DisplayLogicEvaluator();
        var result = evaluator.Evaluate("'Hello World' like '%Test%'", new Dictionary<string, object?>(), NullContext);
        Assert.False(result);
    }

    [Fact]
    public void Evaluate_MalformedExpression_ReturnsFailure()
    {
        var evaluator = new DisplayLogicEvaluator();
        var result = evaluator.Evaluate("true &&", new Dictionary<string, object?>(), NullContext);
        Assert.False(result);
        Assert.False(result.Evaluated);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public void Evaluate_UndefinedCharacters_ReturnsFailure()
    {
        var evaluator = new DisplayLogicEvaluator();
        var result = evaluator.Evaluate("@#$%", new Dictionary<string, object?>(), NullContext);
        Assert.False(result);
    }

    [Fact]
    public void EvaluateBool_ReturnsTrueWhenEvaluatedAndTrue()
    {
        var evaluator = new DisplayLogicEvaluator();
        var result = evaluator.EvaluateBool("true", new Dictionary<string, object?>(), NullContext);
        Assert.True(result);
    }

    [Fact]
    public void EvaluateBool_ReturnsFalseWhenEvaluatedFalse()
    {
        var evaluator = new DisplayLogicEvaluator();
        var result = evaluator.EvaluateBool("false", new Dictionary<string, object?>(), NullContext);
        Assert.False(result);
    }

    [Fact]
    public void EvaluateBool_ReturnsFalseOnParseError()
    {
        var evaluator = new DisplayLogicEvaluator();
        var result = evaluator.EvaluateBool("!!!invalid!!!", new Dictionary<string, object?>(), NullContext);
        Assert.False(result);
    }

    [Fact]
    public void Evaluate_ExpressionTooLong_ReturnsFailure()
    {
        var evaluator = new DisplayLogicEvaluator();
        var longExpr = new string('a', 10000);
        var result = evaluator.Evaluate(longExpr, new Dictionary<string, object?>(), NullContext);
        Assert.False(result);
    }
}
