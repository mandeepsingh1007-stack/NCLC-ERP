namespace Platform.Core.Metadata;

/// <summary>
/// Result of evaluating a single ValRule.
/// </summary>
public sealed class ValRuleResult
{
    public string RuleName { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string? ErrorMessage { get; set; }

    public static ValRuleResult Pass(string ruleName) => new() { RuleName = ruleName, Passed = true };
    public static ValRuleResult Fail(string ruleName, string errorMessage) => new() { RuleName = ruleName, Passed = false, ErrorMessage = errorMessage };
}
