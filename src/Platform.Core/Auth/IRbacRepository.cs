namespace Platform.Core.Auth;

/// <summary>
/// Batch resolution result from RBAC query.
/// Decoupled from Platform.Data to avoid Core → Data dependency.
/// </summary>
public record RbacResolution(
    IReadOnlyDictionary<int, PermissionLevel> WindowPermissions,
    IReadOnlyDictionary<int, PermissionLevel> TablePermissions,
    IReadOnlyDictionary<(int TableId, int ColumnId), PermissionLevel> ColumnPermissions,
    IReadOnlyDictionary<int, string?> RecordFilters,
    IReadOnlyDictionary<int, HashSet<int>> PrivateAccessRecords)
{
    public static RbacResolution Empty => new(
        new Dictionary<int, PermissionLevel>(),
        new Dictionary<int, PermissionLevel>(),
        new Dictionary<(int, int), PermissionLevel>(),
        new Dictionary<int, string?>(),
        new Dictionary<int, HashSet<int>>());
}

/// <summary>
/// Low-level RBAC data access interface.
/// Implementation lives in Platform.Data; interface in Core avoids Core → Data dependency.
/// </summary>
public interface IRbacRepository
{
    /// <summary>
    /// Batch-resolve all permissions for a set of role IDs and client ID.
    /// Private access (SysPrivateAccess) is NOT resolved here — it is user-specific
    /// and must be resolved separately via ResolvePrivateAccessAsync.
    /// </summary>
    Task<RbacResolution> ResolveAsync(int clientId, IEnumerable<int> roleIds);

    /// <summary>
    /// Batch-resolve all permissions for a set of role IDs and client ID,
    /// including user-level private access for the specified userId.
    /// </summary>
    Task<RbacResolution> ResolveAsync(int clientId, IEnumerable<int> roleIds, int userId);

    /// <summary>
    /// Resolve user-level private record access for a set of tables.
    /// Returns a dictionary mapping SysTableId → set of allowed RecordIds.
    /// </summary>
    Task<IReadOnlyDictionary<int, HashSet<int>>> ResolvePrivateAccessAsync(int clientId, int userId);
}

/// <summary>
/// Namespace resolution interface (window/table/column name → ID).
/// Implementation lives in Platform.Data; interface in Core.
/// </summary>
public interface INamespaceRepository
{
    Task<int?> GetWindowIdAsync(string name);
    Task<int?> GetTableIdAsync(string tableName);
    Task<int?> GetColumnIdAsync(string tableName, string columnName);
}
