namespace Platform.Core.Auth;

/// <summary>
/// Login request DTO.
/// </summary>
public record LoginRequest(
    string Username,
    string Password);

/// <summary>
/// Login response DTO with tokens.
/// </summary>
public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    string Username);

/// <summary>
/// Refresh token request DTO.
/// </summary>
public record RefreshRequest(
    string RefreshToken);

/// <summary>
/// Password change request DTO.
/// </summary>
public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);

/// <summary>
/// RBAC permission check request DTO.
/// </summary>
public record PermissionRequest(
    string PermissionType,  // "Window", "Table", "Column", etc.
    string? EntityName = null,  // window name, table name, etc.
    int? EntityId = null,
    string? ColumnName = null);
