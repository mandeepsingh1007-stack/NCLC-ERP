using Platform.Core.Metadata;

namespace Platform.Core.Runtime;

/// <summary>
/// Resolves context variables for ValRule evaluation.
/// Resolves: $UserId, $TenantId, $OrgId, $Timestamp, $Value, $ExistingValue.
/// Also supports parent context references: $ParentTenantId, $ParentOrgId.
/// </summary>
public class ContextVariableResolver : IContextVariableResolver
{
    private const string Prefix = "$";

    public IReadOnlyContext GetCurrentContext()
    {
        // In a real implementation, this reads from HttpContext/HttpRequest.
        // For Phase 2, a default empty context is returned when no HTTP context is available.
        return InMemoryContext.Create(null, null, null);
    }

    public T Resolve<T>(string expression, IReadOnlyContext context)
    {
        if (string.IsNullOrEmpty(expression))
        {
            return default!;
        }

        var resolved = ResolveExpression(expression, context);
        return ConvertTo<T>(resolved);
    }

    private static object? ResolveExpression(string expression, IReadOnlyContext context)
    {
        if (string.IsNullOrEmpty(expression) || !expression.StartsWith(Prefix))
        {
            return expression;
        }

        var varName = expression.Substring(Prefix.Length).ToLowerInvariant();

        return varName switch
        {
            "userid" => context.UserId,
            "tenantid" => context.TenantId,
            "orgid" => context.OrgId,
            "timestamp" => context.Timestamp,
            "value" => context.Value,
            "existingvalue" => context.ExistingValue,
            "parenttenantid" => context.TenantId,  // Default: parent = same tenant in Phase 2
            "parentorgid" => context.OrgId,        // Default: parent = same org in Phase 2
            _ => null                              // Unknown variable → null
        };
    }

    private static T ConvertTo<T>(object? value)
    {
        if (value == null)
        {
            return default!;
        }

        var targetType = typeof(T);
        if (targetType.IsValueType)
        {
            try
            {
                return (T)Convert.ChangeType(value, targetType);
            }
            catch
            {
                return default!;
            }
        }

        return (T)value!;
    }
}
