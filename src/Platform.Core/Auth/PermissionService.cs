using Microsoft.Extensions.Caching.Memory;
using Platform.Core.Runtime;

namespace Platform.Core.Auth;

/// <summary>
/// Permission resolution service implementing hierarchical RBAC.
/// Resolves permissions from the role-access tables (window, table, column, record, private).
/// Uses IMemoryCache for batch resolution results keyed by (ClientId, RoleIds).
/// </summary>
public class PermissionService : IPermissionService
{
    private readonly IMemoryCache _cache;
    private readonly IRbacRepository _rbacRepo;
    private readonly INamespaceRepository _namespaceRepo;
    private readonly IUserRepository _userRepo;
    private readonly IMetadataGraph _metadataGraph;
    private const string CacheKeyPrefix = "rbac:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    public PermissionService(
        IMemoryCache cache,
        IRbacRepository rbacRepo,
        INamespaceRepository namespaceRepo,
        IUserRepository userRepo,
        IMetadataGraph metadataGraph)
    {
        _cache = cache;
        _rbacRepo = rbacRepo;
        _namespaceRepo = namespaceRepo;
        _userRepo = userRepo;
        _metadataGraph = metadataGraph;
    }

    private static string ComputeCacheKey(int clientId, string roleIdsHash)
        => $"{CacheKeyPrefix}{clientId}:{roleIdsHash}";

    private static string HashRoleIds(IEnumerable<int> roleIds)
        => string.Join(",", roleIds.OrderBy(r => r));

    private async Task<RbacResolution> ResolveRbacAsync(int clientId, IEnumerable<int> roleIds)
    {
        var roleIdsList = roleIds.ToList();
        if (roleIdsList.Count == 0)
            return RbacResolution.Empty;

        var key = ComputeCacheKey(clientId, HashRoleIds(roleIdsList));

        return await _cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await _rbacRepo.ResolveAsync(clientId, roleIdsList);
        })!;
    }

    private async Task<(int ClientId, int[] RoleIds)?> GetUserContextAsync(int userId)
    {
        var user = await _userRepo.GetUserByIdAsync(userId);
        if (user == null)
            return null;

        var roleIds = await _userRepo.GetUserRoleIdsAsync(userId);
        return (user.Value.ClientId, roleIds.ToArray());
    }

    public async Task<PermissionResult> CheckWindowAsync(int userId, string windowName, PermissionLevel requiredLevel)
    {
        var userCtx = await GetUserContextAsync(userId);
        if (userCtx == null)
            return new PermissionResult(false, PermissionLevel.None, "User not found.");

        var (clientId, roleIds) = userCtx.Value;

        var windowId = await _namespaceRepo.GetWindowIdAsync(windowName);
        if (windowId == null)
            return new PermissionResult(false, PermissionLevel.None, $"Window '{windowName}' not found.");

        var resolution = await ResolveRbacAsync(clientId, roleIds);

        if (resolution.WindowPermissions.TryGetValue(windowId.Value, out var winLevel))
        {
            var allowed = winLevel >= requiredLevel;
            return new PermissionResult(allowed, winLevel, allowed ? null : "Insufficient window-level permission.");
        }

        return new PermissionResult(false, PermissionLevel.None, "No window access granted for user's roles.");
    }

    public async Task<PermissionResult> CanReadTableAsync(int userId, string tableName, PermissionLevel requiredLevel)
    {
        var userCtx = await GetUserContextAsync(userId);
        if (userCtx == null)
            return new PermissionResult(false, PermissionLevel.None, "User not found.");

        var (clientId, roleIds) = userCtx.Value;

        var tableId = await _namespaceRepo.GetTableIdAsync(tableName);
        if (tableId == null)
            return new PermissionResult(false, PermissionLevel.None, $"Table '{tableName}' not found.");

        var resolution = await ResolveRbacAsync(clientId, roleIds);

        if (resolution.TablePermissions.TryGetValue(tableId.Value, out var tableLevel))
        {
            var allowed = tableLevel >= requiredLevel;
            return new PermissionResult(allowed, tableLevel, allowed ? null : "Insufficient table-level permission.");
        }

        return new PermissionResult(false, PermissionLevel.None, "No table access granted for user's roles.");
    }

    public async Task<PermissionResult> CanWriteTableAsync(int userId, string tableName, PermissionLevel requiredLevel)
    {
        var userCtx = await GetUserContextAsync(userId);
        if (userCtx == null)
            return new PermissionResult(false, PermissionLevel.None, "User not found.");

        var (clientId, roleIds) = userCtx.Value;

        var tableId = await _namespaceRepo.GetTableIdAsync(tableName);
        if (tableId == null)
            return new PermissionResult(false, PermissionLevel.None, $"Table '{tableName}' not found.");

        var resolution = await ResolveRbacAsync(clientId, roleIds);

        if (resolution.TablePermissions.TryGetValue(tableId.Value, out var tableLevel))
        {
            var allowed = tableLevel >= requiredLevel;
            return new PermissionResult(allowed, tableLevel, allowed ? null : "Insufficient table-level write permission.");
        }

        return new PermissionResult(false, PermissionLevel.None, "No table write access granted for user's roles.");
    }

    public async Task<PermissionResult> CheckColumnAsync(int userId, string tableName, string columnName, PermissionLevel requiredLevel)
    {
        var userCtx = await GetUserContextAsync(userId);
        if (userCtx == null)
            return new PermissionResult(false, PermissionLevel.None, "User not found.");

        var (clientId, roleIds) = userCtx.Value;

        var tableId = await _namespaceRepo.GetTableIdAsync(tableName);
        if (tableId == null)
            return new PermissionResult(false, PermissionLevel.None, $"Table '{tableName}' not found.");

        var columnId = await _namespaceRepo.GetColumnIdAsync(tableName, columnName);
        if (columnId == null)
            return new PermissionResult(false, PermissionLevel.None, $"Column '{columnName}' not found in table '{tableName}'.");

        var resolution = await ResolveRbacAsync(clientId, roleIds);

        // Column-level permission takes precedence
        if (resolution.ColumnPermissions.TryGetValue((tableId.Value, columnId.Value), out var colLevel))
        {
            var allowed = colLevel >= requiredLevel;
            return new PermissionResult(allowed, colLevel, allowed ? null : "Insufficient column-level permission.");
        }

        // Fall back to table-level permission
        if (resolution.TablePermissions.TryGetValue(tableId.Value, out var tableLevel))
        {
            return new PermissionResult(tableLevel >= requiredLevel, tableLevel, null);
        }

        return new PermissionResult(false, PermissionLevel.None, "No column or table-level permission.");
    }

    public async Task<IReadOnlySet<string>> GetAllowedColumnsAsync(int userId, string tableName, PermissionLevel requiredLevel)
    {
        var userCtx = await GetUserContextAsync(userId);
        if (userCtx == null)
            return new HashSet<string>();

        var (clientId, roleIds) = userCtx.Value;

        var tableId = await _namespaceRepo.GetTableIdAsync(tableName);
        if (tableId == null)
            return new HashSet<string>();

        var resolution = await ResolveRbacAsync(clientId, roleIds);

        // Get all active columns for the table from metadata
        var allColumns = _metadataGraph.GetColumns(tableName);
        var allowed = new HashSet<string>();

        foreach (var col in allColumns)
        {
            if (!col.IsActive)
                continue;

            // Check column-level permission first
            if (resolution.ColumnPermissions.TryGetValue((tableId.Value, col.SysColumnId), out var colLevel))
            {
                if (colLevel >= requiredLevel)
                    allowed.Add(col.ColumnName);
            }
            else
            {
                // Fall back to table-level permission
                if (resolution.TablePermissions.TryGetValue(tableId.Value, out var tableLevel) &&
                    tableLevel >= requiredLevel)
                {
                    allowed.Add(col.ColumnName);
                }
            }
        }

        return allowed;
    }

    public async Task<string?> GetRecordFilterAsync(int userId, string tableName)
    {
        var userCtx = await GetUserContextAsync(userId);
        if (userCtx == null)
            return null;

        var (clientId, roleIds) = userCtx.Value;

        var tableId = await _namespaceRepo.GetTableIdAsync(tableName);
        if (tableId == null)
            return null;

        var resolution = await ResolveRbacAsync(clientId, roleIds);
        return resolution.RecordFilters.TryGetValue(tableId.Value, out var filter) ? filter : null;
    }

    public async Task<IReadOnlySet<int>> GetPrivateRecordIdsAsync(int userId, string tableName)
    {
        var userCtx = await GetUserContextAsync(userId);
        if (userCtx == null)
            return new HashSet<int>();

        var (clientId, roleIds) = userCtx.Value;

        var tableId = await _namespaceRepo.GetTableIdAsync(tableName);
        if (tableId == null)
            return new HashSet<int>();

        var resolution = await ResolveRbacAsync(clientId, roleIds);
        return resolution.PrivateAccessRecords.TryGetValue(tableId.Value, out var ids)
            ? new HashSet<int>(ids)
            : new HashSet<int>();
    }

    public async Task InvalidateCacheAsync(int userId)
    {
        var userCtx = await GetUserContextAsync(userId);
        if (userCtx == null)
            return;

        var (clientId, roleIds) = userCtx.Value;
        var key = ComputeCacheKey(clientId, HashRoleIds(roleIds));
        _cache.Remove(key);
    }
}
