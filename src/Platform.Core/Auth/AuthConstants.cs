namespace Platform.Core.Auth;

/// <summary>
/// Permission level enum matching the SMALLINT stored in database.
/// 0 = None, 1 = ReadOnly, 2 = ReadWrite, 3 = Create, 4 = FullControl
/// </summary>
public enum PermissionLevel : short
{
    None = 0,
    ReadOnly = 1,
    ReadWrite = 2,
    Create = 3,
    FullControl = 4
}

/// <summary>
/// JWT claim type constants.
/// </summary>
public static class AuthClaimTypes
{
    public const string UserId = "uid";
    public const string Username = "uname";
    public const string ClientId = "cid";
    public const string OrgId = "oid";
    public const string RoleIds = "rids";
    public const string Roles = "roles";
}
