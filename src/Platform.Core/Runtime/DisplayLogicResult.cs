namespace Platform.Core.Runtime;

/// <summary>
/// Result of display logic evaluation.
/// </summary>
public sealed class DisplayLogicResult
{
    public bool Evaluated { get; }
    public bool Value { get; }
    public string? Message { get; }

    private DisplayLogicResult(bool evaluated, bool value, string? message)
    {
        Evaluated = evaluated;
        Value = value;
        Message = message;
    }

    public static DisplayLogicResult True => new(true, true, null);
    public static DisplayLogicResult False => new(true, false, null);
    public static DisplayLogicResult Failure(string message) => new(false, false, message);

    public static implicit operator bool(DisplayLogicResult result) => result.Value;
}
