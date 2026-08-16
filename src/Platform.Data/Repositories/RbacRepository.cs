using Dapper;
using Platform.Core.Auth;
using Npgsql;

namespace Platform.Data.Repositories;

/// <summary>
/// Dapper repository for RBAC permission resolution.
/// Batches all six role-access tables into minimal queries.
/// </summary>
public class RbacRepository : IRbacRepository
{
    private readonly string _connectionString;

    public RbacRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// Batch-resolve all permissions for a set of role IDs and client ID.
    /// Single query per access table type for maximum efficiency.
    /// </summary>
    public async Task<RbacResolution> ResolveAsync(int clientId, IEnumerable<int> roleIds)
        => await ResolveAsync(clientId, roleIds, userId: null);

    public async Task<RbacResolution> ResolveAsync(int clientId, IEnumerable<int> roleIds, int userId)
        => await ResolveAsync(clientId, roleIds, userId: (int?)userId);

    private async Task<RbacResolution> ResolveAsync(int clientId, IEnumerable<int> roleIds, int? userId)
    {
        var roleList = roleIds.ToList();
        if (roleList.Count == 0)
            return RbacResolution.Empty;

        using var conn = new NpgsqlConnection(_connectionString);

        // Window access
        var windowRows = await conn.QueryAsync<(int SysWindowId, int Permission)>(
            @"SELECT ""SysWindow_ID"", ""Permission""
              FROM ""SysRoleWindowAccess""
              WHERE ""SysClient_ID"" = @ClientId AND ""SysRole_ID"" = ANY(@RoleIds)",
            new { ClientId = clientId, RoleIds = roleList });
        var windowPermissions = windowRows.ToDictionary(
            r => r.SysWindowId,
            r => (PermissionLevel)r.Permission);

        // Table access
        var tableRows = await conn.QueryAsync<(int SysTableId, int Permission)>(
            @"SELECT ""SysTable_ID"", ""Permission""
              FROM ""SysRoleTableAccess""
              WHERE ""SysClient_ID"" = @ClientId AND ""SysRole_ID"" = ANY(@RoleIds)",
            new { ClientId = clientId, RoleIds = roleList });
        var tablePermissions = tableRows.ToDictionary(
            r => r.SysTableId,
            r => (PermissionLevel)r.Permission);

        // Column access
        var columnRows = await conn.QueryAsync<(int SysTableId, int SysColumnId, int Permission)>(
            @"SELECT ""SysTable_ID"", ""SysColumn_ID"", ""Permission""
              FROM ""SysRoleColumnAccess""
              WHERE ""SysClient_ID"" = @ClientId AND ""SysRole_ID"" = ANY(@RoleIds)",
            new { ClientId = clientId, RoleIds = roleList });
        var columnPermissions = columnRows.ToDictionary(
            r => (r.SysTableId, r.SysColumnId),
            r => (PermissionLevel)r.Permission);

        // Record-level filters
        var recordRows = await conn.QueryAsync<(int SysTableId, string FilterExpression)>(
            @"SELECT ""SysTable_ID"", ""FilterExpression""
              FROM ""SysRecordAccess""
              WHERE ""SysClient_ID"" = @ClientId AND ""SysRole_ID"" = ANY(@RoleIds)",
            new { ClientId = clientId, RoleIds = roleList });
        var recordFilters = recordRows
            .Where(r => !string.IsNullOrWhiteSpace(r.FilterExpression))
            .GroupBy(r => r.SysTableId)
            .ToDictionary(
                g => g.Key,
                g => string.Join(" AND ", g.Select(r => $"({r.FilterExpression})")));

        // Private record access — role-level (legacy, included in cached resolution)
        // Note: User-level private access is resolved separately via ResolvePrivateAccessAsync.
        var privateAccess = new Dictionary<int, HashSet<int>>();

        return new RbacResolution(
            windowPermissions,
            tablePermissions,
            columnPermissions,
            recordFilters,
            privateAccess);
    }

    public async Task<IReadOnlyDictionary<int, HashSet<int>>> ResolvePrivateAccessAsync(int clientId, int userId)
    {
        using var conn = new NpgsqlConnection(_connectionString);

        var privateRows = await conn.QueryAsync<(int SysTableId, int RecordId)>(
            @"SELECT ""SysTable_ID"", ""RecordId""
              FROM ""SysPrivateAccess""
              WHERE ""SysClient_ID"" = @ClientId AND ""SysUser_ID"" = @UserId",
            new { ClientId = clientId, UserId = userId });

        var result = privateRows
            .GroupBy(r => r.SysTableId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => r.RecordId).ToHashSet());

        return result;
    }
}
