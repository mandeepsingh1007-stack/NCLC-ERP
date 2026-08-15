using Platform.Core.Cache;
using Platform.Core.Metadata;

namespace Platform.Core.Runtime;

/// <summary>
/// Orchestrates the PO lifecycle: hooks → validation → persistence → events.
/// Respects the hierarchy: IPersistentObject → PO → X_Table → M_Table.
///
/// Lifecycle order:
/// CREATE: BeforeCreate → Validation → AfterCreate → Persist → PublishEvent
/// UPDATE: BeforeUpdate → Change Validation → AfterUpdate → Persist → PublishEvent
/// DELETE: BeforeDelete → Persist → AfterDelete → PublishEvent
///
/// Validation is enforced by this manager before any persist operation.
/// </summary>
public class POLifecycleManager
{
    private readonly IEnumerable<IPOLifecycleHooks> _hooks;
    private readonly POValidator _validator;
    private readonly ICacheInvalidationService _cacheInvalidation;
    private readonly IMetadataGraph _metadataGraph;

    public POLifecycleManager(
        IEnumerable<IPOLifecycleHooks> hooks,
        POValidator validator,
        ICacheInvalidationService cacheInvalidation,
        IMetadataGraph metadataGraph)
    {
        _hooks = hooks;
        _validator = validator;
        _cacheInvalidation = cacheInvalidation;
        _metadataGraph = metadataGraph;
    }

    /// <summary>
    /// Execute the full CREATE lifecycle for a persistent object.
    /// </summary>
    public async Task<HookResult> CreateAsync(
        IPersistentObject po,
        IReadOnlyContext context,
        Action<int> persist)
    {
        // 1. BeforeCreate hooks
        foreach (var hook in _hooks)
        {
            var result = await hook.BeforeCreateAsync(po, context);
            if (!result.Allowed)
            {
                return HookResult.Veto(result.Message ?? "BeforeCreate hook vetoed.");
            }
        }

        // 2. Validation — ENFORCED before persist
        var valResult = _validator.ValidatePO(po, context);
        if (!valResult.IsSuccess)
        {
            return HookResult.Veto($"Validation failed: {string.Join("; ", valResult.Errors)}");
        }

        // 3. AfterCreate hooks (side effects)
        foreach (var hook in _hooks)
        {
            var result = await hook.AfterCreateAsync(po, context);
            if (!result.Allowed)
            {
                return HookResult.Veto(result.Message ?? "AfterCreate hook vetoed.");
            }
        }

        // 4. Persist
        persist(po.SysTableId);

        // 5. Publish DictionaryChangedEvent → triggers cache invalidation
        var tableInfo = GetTableInfo(po);
        if (tableInfo != null)
        {
            var evt = new DictionaryChangedEvent("Table", tableInfo.SysTableId, tableInfo.TableName, "Created");
            await _cacheInvalidation.InvalidateAsync(evt);
        }

        return HookResult.Allow();
    }

    /// <summary>
    /// Execute the full UPDATE lifecycle for a persistent object.
    /// </summary>
    public async Task<HookResult> UpdateAsync(
        IPersistentObject po,
        IReadOnlyDictionary<string, object?> changes,
        IReadOnlyContext context,
        Action persist)
    {
        // 1. BeforeUpdate hooks
        foreach (var hook in _hooks)
        {
            var result = await hook.BeforeUpdateAsync(po, changes, context);
            if (!result.Allowed)
            {
                return HookResult.Veto(result.Message ?? "BeforeUpdate hook vetoed.");
            }
        }

        // 2. Change validation — ENFORCED before persist
        var valResult = _validator.ValidatePO(po, context);
        if (!valResult.IsSuccess)
        {
            return HookResult.Veto($"Validation failed: {string.Join("; ", valResult.Errors)}");
        }

        // 3. AfterUpdate hooks
        foreach (var hook in _hooks)
        {
            var result = await hook.AfterUpdateAsync(po, changes, context);
            if (!result.Allowed)
            {
                return HookResult.Veto(result.Message ?? "AfterUpdate hook vetoed.");
            }
        }

        // 4. Persist
        persist();

        // 5. Publish DictionaryChangedEvent
        var tableInfo = GetTableInfo(po);
        if (tableInfo != null)
        {
            var evt = new DictionaryChangedEvent("Table", tableInfo.SysTableId, tableInfo.TableName, "Updated");
            await _cacheInvalidation.InvalidateAsync(evt);
        }

        return HookResult.Allow();
    }

    /// <summary>
    /// Execute the full DELETE lifecycle for a persistent object.
    /// </summary>
    public async Task<HookResult> DeleteAsync(
        IPersistentObject po,
        IReadOnlyContext context,
        Action persist)
    {
        // 1. BeforeDelete hooks
        foreach (var hook in _hooks)
        {
            var result = await hook.BeforeDeleteAsync(po, context);
            if (!result.Allowed)
            {
                return HookResult.Veto(result.Message ?? "BeforeDelete hook vetoed.");
            }
        }

        // 2. Persist
        persist();

        // 3. AfterDelete hooks
        foreach (var hook in _hooks)
        {
            var result = await hook.AfterDeleteAsync(po, context);
            if (!result.Allowed)
            {
                return HookResult.Veto(result.Message ?? "AfterDelete hook vetoed.");
            }
        }

        // 4. Publish DictionaryChangedEvent
        var tableInfo = GetTableInfo(po);
        if (tableInfo != null)
        {
            var evt = new DictionaryChangedEvent("Table", tableInfo.SysTableId, tableInfo.TableName, "Deleted");
            await _cacheInvalidation.InvalidateAsync(evt);
        }

        return HookResult.Allow();
    }

    /// <summary>
    /// Execute the LOAD lifecycle for a persistent object.
    /// </summary>
    public async Task<HookResult> LoadAsync(
        IPersistentObject po,
        int id,
        IReadOnlyContext context,
        Action<int> load)
    {
        foreach (var hook in _hooks)
        {
            var result = await hook.OnLoadAsync(po, context);
            if (!result.Allowed)
            {
                return HookResult.Veto(result.Message ?? "OnLoad hook vetoed.");
            }
        }

        load(id);
        return HookResult.Allow();
    }

    private TableMetadata? GetTableInfo(IPersistentObject po)
    {
        return _metadataGraph.GetTableById(po.SysTableId);
    }
}
