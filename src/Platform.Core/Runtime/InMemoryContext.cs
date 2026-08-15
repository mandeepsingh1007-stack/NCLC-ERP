using Platform.Core.Metadata;

namespace Platform.Core.Runtime;

/// <summary>
/// Immutable context implementation. Created server-side from JWT claims.
/// Cannot be mutated — all properties are read-only.
/// </summary>
public sealed class InMemoryContext : IReadOnlyContext
{
    public string? UserId { get; }
    public string? TenantId { get; }
    public string? OrgId { get; }
    public DateTime Timestamp { get; }
    public object? Value { get; }
    public object? ExistingValue { get; }
    public IReadOnlyDictionary<string, object?> Extensions { get; }
    public string? TenantPredicate { get; }
    public string? OrgPredicate { get; }

    private InMemoryContext(
        string? userId,
        string? tenantId,
        string? orgId,
        DateTime timestamp,
        object? value,
        object? existingValue,
        IReadOnlyDictionary<string, object?> extensions,
        string? tenantPredicate = null,
        string? orgPredicate = null)
    {
        UserId = userId;
        TenantId = tenantId;
        OrgId = orgId;
        Timestamp = timestamp;
        Value = value;
        ExistingValue = existingValue;
        Extensions = extensions;
        TenantPredicate = tenantPredicate;
        OrgPredicate = orgPredicate;
    }

    /// <summary>
    /// Create a base context from JWT claims.
    /// </summary>
    public static InMemoryContext Create(string? userId, string? tenantId, string? orgId)
    {
        return new InMemoryContext(
            userId, tenantId, orgId, DateTime.UtcNow, null, null,
            new Dictionary<string, object?>());
    }

    /// <summary>
    /// Create a context with tenant/org predicates for isolation.
    /// </summary>
    public static InMemoryContext CreateWithTenantIsolation(
        string? userId,
        string? tenantId,
        string? orgId,
        string? tenantPredicate,
        string? orgPredicate)
    {
        return new InMemoryContext(
            userId, tenantId, orgId, DateTime.UtcNow, null, null,
            new Dictionary<string, object?>(), tenantPredicate, orgPredicate);
    }

    /// <summary>
    /// Create a context for validation with a specific value.
    /// </summary>
    public InMemoryContext WithValue(object? value, object? existingValue = null)
    {
        return new InMemoryContext(
            UserId, TenantId, OrgId, Timestamp, value, existingValue, Extensions,
            TenantPredicate, OrgPredicate);
    }

    /// <summary>
    /// Create a context with extensions.
    /// </summary>
    public InMemoryContext WithExtensions(IReadOnlyDictionary<string, object?> extensions)
    {
        return new InMemoryContext(
            UserId, TenantId, OrgId, Timestamp, Value, ExistingValue,
            extensions ?? Extensions, TenantPredicate, OrgPredicate);
    }
}
