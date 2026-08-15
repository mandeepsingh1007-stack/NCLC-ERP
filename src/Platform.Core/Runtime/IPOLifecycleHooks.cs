using Platform.Core.Metadata;

namespace Platform.Core.Runtime;

/// <summary>
/// Lifecycle hooks invoked around PO persistence operations.
/// Can veto operations (BeforeCreate/BeforeUpdate/BeforeDelete) or perform side effects (After*).
/// </summary>
public interface IPOLifecycleHooks
{
    Task<HookResult> BeforeCreateAsync(object po, IReadOnlyContext context);
    Task<HookResult> AfterCreateAsync(object po, IReadOnlyContext context);
    Task<HookResult> BeforeUpdateAsync(object po, IReadOnlyDictionary<string, object?> changes, IReadOnlyContext context);
    Task<HookResult> AfterUpdateAsync(object po, IReadOnlyDictionary<string, object?> changes, IReadOnlyContext context);
    Task<HookResult> BeforeDeleteAsync(object po, IReadOnlyContext context);
    Task<HookResult> AfterDeleteAsync(object po, IReadOnlyContext context);
    Task<HookResult> OnLoadAsync(object po, IReadOnlyContext context);
}
