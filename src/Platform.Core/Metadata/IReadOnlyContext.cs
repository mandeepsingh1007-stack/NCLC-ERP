namespace Platform.Core.Metadata;

/// <summary>
/// Immutable runtime context propagated through validation and evaluation.
/// Created once in authentication middleware from JWT claims.
/// Cannot be mutated during validation pipeline.
/// </summary>
public interface IReadOnlyContext
{
    string? UserId { get; }
    string? TenantId { get; }
    string? OrgId { get; }
    DateTime Timestamp { get; }
    object? Value { get; }            // the value being validated
    object? ExistingValue { get; }    // original value for update hooks
    IReadOnlyDictionary<string, object?> Extensions { get; }

    /// <summary>
    /// Optional SQL WHERE predicate for tenant isolation (e.g., "TenantId = @TenantId").
    /// Populated from context.Extensions["TenantPredicate"] at context creation time.
    /// When set and TenantId is non-null, automatically injected into SQL execution.
    /// </summary>
    string? TenantPredicate { get; }

    /// <summary>
    /// Optional SQL WHERE predicate for org isolation (e.g., "OrgId = @OrgId").
    /// Populated from context.Extensions["OrgPredicate"] at context creation time.
    /// </summary>
    string? OrgPredicate { get; }
}
