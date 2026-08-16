namespace Platform.Core.Auth;

/// <summary>
/// Result of a permission check.
/// </summary>
public record PermissionResult(
    bool Allowed,
    PermissionLevel Level,
    string? Reason);

/// <summary>
/// Single row of resolved RBAC data for a user.
/// Cached in IMemoryCache keyed by (ClientId, RoleIds).
/// </summary>
public record RbacCacheEntry(
    int ClientId,
    string RoleIdsHash,
    IReadOnlyDictionary<string, PermissionLevel> WindowPermissions,
    IReadOnlyDictionary<int, PermissionLevel> TablePermissions,
    IReadOnlyDictionary<(int TableId, int ColumnId), PermissionLevel> ColumnPermissions,
    IReadOnlyDictionary<int, string?> RecordFilters,
    IReadOnlySet<int> PrivateRecordIds);

/// <summary>
/// Resolves user permissions from the 14 security metadata tables.
/// Batches all resolution into minimal queries and caches results.
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// Check if the user has at least the required permission level for a window.
    /// </summary>
    Task<PermissionResult> CheckWindowAsync(int userId, string windowName, PermissionLevel requiredLevel);

    /// <summary>
    /// Check if the user can read a specific table.
    /// </summary>
    Task<PermissionResult> CanReadTableAsync(int userId, string tableName, PermissionLevel requiredLevel);

    /// <summary>
    /// Check if the user can write to a specific table.
    /// </summary>
    Task<PermissionResult> CanWriteTableAsync(int userId, string tableName, PermissionLevel requiredLevel);

    /// <summary>
    /// Check if the user can access a specific column (for projection/write filtering).
    /// </summary>
    Task<PermissionResult> CheckColumnAsync(int userId, string tableName, string columnName, PermissionLevel requiredLevel);

    /// <summary>
    /// Get allowed column names for a table (for GET projection filtering).
    /// Returns empty set if no access.
    /// </summary>
    Task<IReadOnlySet<string>> GetAllowedColumnsAsync(int userId, string tableName, PermissionLevel requiredLevel);

    /// <summary>
    /// Get record-level filter expressions for a table (for WHERE clause injection).
    /// Returns null if no record-level filter applies.
    /// </summary>
    Task<string?> GetRecordFilterAsync(int userId, string tableName);

    /// <summary>
    /// Get private record IDs visible to the user for a table.
    /// Used for SysPrivateAccess enforcement.
    /// </summary>
    Task<IReadOnlySet<int>> GetPrivateRecordIdsAsync(int userId, string tableName);

    /// <summary>
    /// Invalidate the RBAC cache for a user.
    /// Called after successful metadata mutations.
    /// </summary>
    Task InvalidateCacheAsync(int userId);
}
