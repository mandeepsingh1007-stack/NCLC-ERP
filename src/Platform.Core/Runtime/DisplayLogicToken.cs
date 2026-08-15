namespace Platform.Core.Runtime;

/// <summary>
/// Token types for the display logic expression parser (ADR-0006).
/// </summary>
internal enum DisplayLogicTokenType
{
    Unknown,
    FieldRef,       // $FieldName
    Number,         // 42, 3.14
    String,         // 'Active'
    Boolean,        // true, false
    Null,           // null
    Eq,             // ==
    Ne,             // != or <>
    Lt,             // <
    Gt,             // >
    Le,             // <=
    Ge,             // >=
    And,            // &&
    Or,             // ||
    Not,            // !
    In,             // in
    NotIn,          // not in
    Like,           // like
    NotLike,        // not like
    Empty,          // empty
    NotEmpty,       // not empty
    LParen,         // (
    RParen,         // )
    Comma,          // ,
    EOF
}

internal readonly struct DisplayLogicToken
{
    public DisplayLogicTokenType Type { get; }
    public string Text { get; }

    public DisplayLogicToken(DisplayLogicTokenType type, string text)
    {
        Type = type;
        Text = text;
    }

    public override string ToString() => $"[{Type}] '{Text}'";
}
