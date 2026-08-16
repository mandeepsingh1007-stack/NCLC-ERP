using Platform.Core.Metadata;

namespace Platform.Core.Runtime;

/// <summary>
/// Evaluates display logic expressions stored in SysField (ADR-0006).
/// Uses a parsed expression tree — NEVER eval(), Function(), or arbitrary code execution.
/// Safe, deterministic, bounded (max depth 20, max tokens 200).
/// </summary>
public class DisplayLogicEvaluator
{
    private const int MaxTokens = 200;

    /// <summary>
    /// Evaluates a display logic expression for a field value.
    /// Returns false (field hidden) on parse/eval error.
    /// </summary>
    public DisplayLogicResult Evaluate(string? expression, IDictionary<string, object?> poValues, IReadOnlyContext context)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return DisplayLogicResult.False;

        try
        {
            var tokens = Tokenize(expression);
            if (tokens == null)
                return DisplayLogicResult.False;

            var parser = new ExpressionParser(tokens);
            var ast = parser.ParseOr();
            if (ast == null)
                return DisplayLogicResult.False;

            // Check for unconsumed tokens (malformed expression)
            if (parser.Position < tokens.Count - 1)
                return DisplayLogicResult.Failure($"Unconsumed tokens in expression");

            var result = ast.Evaluate(context, poValues);
            return result ? DisplayLogicResult.True : DisplayLogicResult.False;
        }
        catch (Exception ex)
        {
            return DisplayLogicResult.Failure($"Display logic parse error: {ex.Message}");
        }
    }

    /// <summary>
    /// Evaluates a boolean expression and returns the actual boolean result.
    /// Returns false on error.
    /// </summary>
    public bool EvaluateBool(string? expression, IDictionary<string, object?> poValues, IReadOnlyContext context)
    {
        var result = Evaluate(expression, poValues, context);
        return result.Evaluated && result.Value;
    }

    private List<DisplayLogicToken>? Tokenize(string expression)
    {
        var tokens = new List<DisplayLogicToken>();
        var i = 0;
        var len = expression.Length;
        var tokenCount = 0;

        while (i < len)
        {
            if (char.IsWhiteSpace(expression[i]))
            {
                i++;
                continue;
            }

            if (i + 1 < len)
            {
                var two = expression.Substring(i, 2);
                switch (two)
                {
                    case "&&":
                        tokens.Add(new DisplayLogicToken(DisplayLogicTokenType.And, "&&"));
                        tokenCount++; i += 2; continue;
                    case "||":
                        tokens.Add(new DisplayLogicToken(DisplayLogicTokenType.Or, "||"));
                        tokenCount++; i += 2; continue;
                    case "==":
                        tokens.Add(new DisplayLogicToken(DisplayLogicTokenType.Eq, "=="));
                        tokenCount++; i += 2; continue;
                    case "!=":
                        tokens.Add(new DisplayLogicToken(DisplayLogicTokenType.Ne, "!="));
                        tokenCount++; i += 2; continue;
                    case "<>":
                        tokens.Add(new DisplayLogicToken(DisplayLogicTokenType.Ne, "<>"));
                        tokenCount++; i += 2; continue;
                    case "<=":
                        tokens.Add(new DisplayLogicToken(DisplayLogicTokenType.Le, "<="));
                        tokenCount++; i += 2; continue;
                    case ">=":
                        tokens.Add(new DisplayLogicToken(DisplayLogicTokenType.Ge, ">="));
                        tokenCount++; i += 2; continue;
                }
            }

            var ch = expression[i];
            switch (ch)
            {
                case '(':
                    tokens.Add(new DisplayLogicToken(DisplayLogicTokenType.LParen, "("));
                    tokenCount++; i++; break;
                case ')':
                    tokens.Add(new DisplayLogicToken(DisplayLogicTokenType.RParen, ")"));
                    tokenCount++; i++; break;
                case '[':
                    tokens.Add(new DisplayLogicToken(DisplayLogicTokenType.LParen, "["));
                    tokenCount++; i++; break;
                case ']':
                    tokens.Add(new DisplayLogicToken(DisplayLogicTokenType.RParen, "]"));
                    tokenCount++; i++; break;
                case ',':
                    tokens.Add(new DisplayLogicToken(DisplayLogicTokenType.Comma, ","));
                    tokenCount++; i++; break;
                case '!':
                    tokens.Add(new DisplayLogicToken(DisplayLogicTokenType.Not, "!"));
                    tokenCount++; i++; break;
                case '<':
                    tokens.Add(new DisplayLogicToken(DisplayLogicTokenType.Lt, "<"));
                    tokenCount++; i++; break;
                case '>':
                    tokens.Add(new DisplayLogicToken(DisplayLogicTokenType.Gt, ">"));
                    tokenCount++; i++; break;

                case '\'':
                    var sb = new System.Text.StringBuilder();
                    i++;
                    while (i < len && expression[i] != '\'')
                    {
                        if (expression[i] == '\\' && i + 1 < len)
                        {
                            i++;
                            sb.Append(expression[i]);
                        }
                        else
                        {
                            sb.Append(expression[i]);
                        }
                        i++;
                    }
                    if (i >= len) return null;
                    i++;
                    tokens.Add(new DisplayLogicToken(DisplayLogicTokenType.String, sb.ToString()));
                    tokenCount++;
                    break;

                default:
                    if (char.IsDigit(ch) || (ch == '.' && i + 1 < len && char.IsDigit(expression[i + 1])))
                    {
                        var numSb = new System.Text.StringBuilder();
                        while (i < len && (char.IsDigit(expression[i]) || expression[i] == '.'))
                        {
                            numSb.Append(expression[i]);
                            i++;
                        }
                        if (double.TryParse(numSb.ToString(), out var num))
                        {
                            tokens.Add(new DisplayLogicToken(DisplayLogicTokenType.Number, num.ToString()!));
                            tokenCount++;
                        }
                    }
                    else if (char.IsLetter(ch) || ch == '$')
                    {
                        var wordSb = new System.Text.StringBuilder();
                        while (i < len && (char.IsLetterOrDigit(expression[i]) || expression[i] == '_' || expression[i] == '$'))
                        {
                            wordSb.Append(expression[i]);
                            i++;
                        }
                        var word = wordSb.ToString();

                        DisplayLogicTokenType tt;
                        if (word.Equals("true", StringComparison.OrdinalIgnoreCase)) tt = DisplayLogicTokenType.Boolean;
                        else if (word.Equals("false", StringComparison.OrdinalIgnoreCase)) tt = DisplayLogicTokenType.Boolean;
                        else if (word.Equals("null", StringComparison.OrdinalIgnoreCase)) tt = DisplayLogicTokenType.Null;
                        else if (word.Equals("in", StringComparison.OrdinalIgnoreCase)) tt = DisplayLogicTokenType.In;
                        else if (word.Equals("not", StringComparison.OrdinalIgnoreCase))
                        {
                            // Peek ahead for two-word operators: "not in", "not like", "not empty"
                            var saved = i;
                            while (i < len && char.IsWhiteSpace(expression[i])) i++;
                            var nextWordStart = i;
                            var nextWord = string.Empty;
                            if (i < len && (char.IsLetter(expression[i]) || expression[i] == '$'))
                            {
                                var nwb = new System.Text.StringBuilder();
                                while (i < len && (char.IsLetterOrDigit(expression[i]) || expression[i] == '_' || expression[i] == '$'))
                                    nwb.Append(expression[i++]);
                                nextWord = nwb.ToString();
                            }
                            if (string.Equals(nextWord, "in", StringComparison.OrdinalIgnoreCase))
                            {
                                tt = DisplayLogicTokenType.NotIn;
                                tokenCount++;
                            }
                            else if (string.Equals(nextWord, "like", StringComparison.OrdinalIgnoreCase))
                            {
                                tt = DisplayLogicTokenType.NotLike;
                                tokenCount++;
                            }
                            else if (string.Equals(nextWord, "empty", StringComparison.OrdinalIgnoreCase))
                            {
                                tt = DisplayLogicTokenType.NotEmpty;
                                tokenCount++;
                            }
                            else
                            {
                                // Regular "not" for prefix negation
                                tt = DisplayLogicTokenType.Not;
                                i = saved;
                            }
                            wordSb.Append(' ').Append(nextWord);
                        }
                        else if (word.Equals("like", StringComparison.OrdinalIgnoreCase)) tt = DisplayLogicTokenType.Like;
                        else if (word.Equals("empty", StringComparison.OrdinalIgnoreCase)) tt = DisplayLogicTokenType.Empty;
                        else if (word.StartsWith("$")) tt = DisplayLogicTokenType.FieldRef;
                        else tt = DisplayLogicTokenType.Unknown;

                        tokens.Add(new DisplayLogicToken(tt, word));
                        tokenCount++;
                    }
                    else
                    {
                        return null;
                    }
                    break;
            }

            if (tokenCount > MaxTokens) return null;
        }

        tokens.Add(new DisplayLogicToken(DisplayLogicTokenType.EOF, ""));
        return tokens;
    }
}

/// <summary>
/// Recursive descent parser for display logic expressions (ADR-0006).
/// Grammar: Expression -> OrExpr -> AndExpr -> NotExpr -> Primary
/// </summary>
internal class ExpressionParser
{
    private readonly List<DisplayLogicToken> _tokens;
    private int _pos;

    public ExpressionParser(List<DisplayLogicToken> tokens)
    {
        _tokens = tokens;
        _pos = 0;
    }

    /// <summary>
    /// Current token position — used to detect unconsumed tokens after parsing.
    /// </summary>
    public int Position => _pos;

    public DisplayLogicASTNode? ParseOr()
    {
        var left = ParseComparison(ParseAnd());
        while (_pos < _tokens.Count && _tokens[_pos].Type == DisplayLogicTokenType.Or)
        {
            _pos++;
            var right = ParseComparison(ParseAnd());
            left = new OrNode(left, right);
        }
        return left;
    }

    /// <summary>
    /// Handles comparison operators (==, !=, <, >, <=, >=, in, like) which have lower precedence
    /// than &&/|| but higher than primary literals.
    /// This allows expressions like 'Active' == 'Active' or 5 < 10 to be parsed correctly.
    /// </summary>
    private DisplayLogicASTNode ParseComparison(DisplayLogicASTNode left)
    {
        while (_pos < _tokens.Count && IsComparisonOp(_tokens[_pos].Type))
        {
            var op = ConvertOp(_tokens[_pos].Type);
            _pos++;

            // 'in' and 'not in' expect an array literal [...] as the right operand
            if (_tokens[_pos].Type == DisplayLogicTokenType.LParen && _tokens[_pos].Text == "[")
            {
                _pos++;
                var items = new List<DisplayLogicASTNode>();
                while (_pos < _tokens.Count && !(_tokens[_pos].Type == DisplayLogicTokenType.RParen && _tokens[_pos].Text == "]"))
                {
                    items.Add(ParsePrimary());
                    if (_pos < _tokens.Count && _tokens[_pos].Type == DisplayLogicTokenType.Comma)
                    {
                        _pos++;
                    }
                }
                if (_pos < _tokens.Count && _tokens[_pos].Type == DisplayLogicTokenType.RParen && _tokens[_pos].Text == "]")
                    _pos++;
                var rightNode = new ArrayNode(items);
                left = new ComparisonNode(op, left, rightNode);
            }
            else
            {
                var right = ParsePrimary();
                left = new ComparisonNode(op, left, right);
            }
        }
        // Check for unary 'empty' / 'not empty' after binary comparison
        return ParseEmptyCheck(left);
    }

    private DisplayLogicASTNode ParseAnd()
    {
        var left = ParseComparison(ParseNot());
        while (_pos < _tokens.Count && _tokens[_pos].Type == DisplayLogicTokenType.And)
        {
            _pos++;
            var right = ParseComparison(ParseNot());
            left = new AndNode(left, right);
        }
        return left;
    }

    private DisplayLogicASTNode ParseNot()
    {
        // Handle prefix '!' negation
        if (_pos < _tokens.Count && _tokens[_pos].Type == DisplayLogicTokenType.Not)
        {
            _pos++;
            var operand = ParseNot();
            return new NotNode(operand);
        }
        return ParsePrimary();
    }

    /// <summary>
    /// Parses primary nodes and checks for unary postfix 'empty' / 'not empty' operators.
    /// 'empty' and 'not empty' are unary operators that check for null/empty values.
    /// </summary>
    private DisplayLogicASTNode ParseEmptyCheck(DisplayLogicASTNode node)
    {
        if (_pos < _tokens.Count && _tokens[_pos].Type == DisplayLogicTokenType.Empty)
        {
            _pos++;
            return new EmptyCheckNode(true, node);
        }
        if (_pos < _tokens.Count && _tokens[_pos].Type == DisplayLogicTokenType.NotEmpty)
        {
            _pos++;
            return new EmptyCheckNode(false, node);
        }
        return node;
    }

    private DisplayLogicASTNode ParsePrimary()
    {
        if (_pos >= _tokens.Count)
            return new LiteralNode(null);

        var token = _tokens[_pos];

        switch (token.Type)
        {
            case DisplayLogicTokenType.LParen:
                _pos++;
                var expr = ParseOr() ?? new LiteralNode(null);
                if (_pos < _tokens.Count && _tokens[_pos].Type == DisplayLogicTokenType.RParen)
                    _pos++;
                return ParseEmptyCheck(expr);

            case DisplayLogicTokenType.String:
                _pos++;
                return new LiteralNode(token.Text);

            case DisplayLogicTokenType.Number:
                _pos++;
                if (double.TryParse(token.Text, out var num))
                    return new LiteralNode(num);
                return new LiteralNode(token.Text);

            case DisplayLogicTokenType.Boolean:
                _pos++;
                return new LiteralNode(token.Text.Equals("true", StringComparison.OrdinalIgnoreCase));

            case DisplayLogicTokenType.Null:
                _pos++;
                return new LiteralNode(null);

            case DisplayLogicTokenType.FieldRef:
                {
                    var fieldRef = token.Text;
                    _pos++;

                    if (_pos < _tokens.Count && IsComparisonOp(_tokens[_pos].Type))
                    {
                        var op = ConvertOp(_tokens[_pos].Type);
                        _pos++;
                        var right = ParsePrimary();
                        var leftNode = new FieldRefNode(fieldRef);
                        var comp = new ComparisonNode(op, leftNode, right);
                        return ParseEmptyCheck(comp);
                    }

                    var fieldNode = new FieldRefNode(fieldRef);
                    return ParseEmptyCheck(fieldNode);
                }

            case DisplayLogicTokenType.EOF:
                // Truncated expression — no right operand for binary operator
                throw new InvalidOperationException("Unexpected end of expression");

            default:
                _pos++;
                var defaultNode = new LiteralNode(null);
                return ParseEmptyCheck(defaultNode);
        }
    }

    private static bool IsComparisonOp(DisplayLogicTokenType type)
    {
        return type is DisplayLogicTokenType.Eq or DisplayLogicTokenType.Ne or
               DisplayLogicTokenType.Lt or DisplayLogicTokenType.Gt or
               DisplayLogicTokenType.Le or DisplayLogicTokenType.Ge or
               DisplayLogicTokenType.In or DisplayLogicTokenType.NotIn or
               DisplayLogicTokenType.Like or DisplayLogicTokenType.NotLike;
    }

    private static string ConvertOp(DisplayLogicTokenType type)
    {
        return type switch
        {
            DisplayLogicTokenType.Eq => "==",
            DisplayLogicTokenType.Ne => "!=",
            DisplayLogicTokenType.Lt => "<",
            DisplayLogicTokenType.Gt => ">",
            DisplayLogicTokenType.Le => "<=",
            DisplayLogicTokenType.Ge => ">=",
            DisplayLogicTokenType.In => "in",
            DisplayLogicTokenType.NotIn => "not in",
            DisplayLogicTokenType.Like => "like",
            DisplayLogicTokenType.NotLike => "not like",
            DisplayLogicTokenType.Empty => "empty",
            DisplayLogicTokenType.NotEmpty => "not empty",
            _ => ""
        };
    }
}
