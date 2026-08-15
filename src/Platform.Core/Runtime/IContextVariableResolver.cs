using Platform.Core.Metadata;

namespace Platform.Core.Runtime;

/// <summary>
/// Resolves context variables ($UserId, $TenantId, etc.) for ValRule evaluation.
/// Scoped to HTTP request lifetime — reads from HttpContext.
/// </summary>
public interface IContextVariableResolver
{
    IReadOnlyContext GetCurrentContext();
    T Resolve<T>(string expression, IReadOnlyContext context);
}
