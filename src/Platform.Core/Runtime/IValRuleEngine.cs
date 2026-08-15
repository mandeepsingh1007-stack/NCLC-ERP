using Platform.Core.Metadata;

namespace Platform.Core.Runtime;

/// <summary>
/// Secure ValRule evaluation engine.
/// Phase 2 supports: SQL (SELECT-only, parameterized), REGEX (100ms timeout, no options).
/// LAMBDA and SCRIPT are NOT supported in Phase 2 (deferred).
/// </summary>
public interface IValRuleEngine
{
    ValRuleResult Evaluate(SysValRule rule, object? value, IReadOnlyContext context);
    IReadOnlyList<ValRuleResult> EvaluateBatch(string tableName, object? value, IReadOnlyContext context);
}
