namespace Platform.Core.Auth;

/// <summary>
/// Service for authentication operations (login, refresh, logout).
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Authenticate user with username/password. Returns JWT tokens.
    /// </summary>
    Task<LoginResponse> LoginAsync(LoginRequest request, string ipAddress, string userAgent);

    /// <summary>
    /// Refresh access token using a valid refresh token.
    /// </summary>
    Task<LoginResponse> RefreshAsync(RefreshRequest request, string ipAddress, string userAgent);

    /// <summary>
    /// Logout: revoke the refresh token and add JWT jti to deny list.
    /// </summary>
    Task LogoutAsync(string refreshToken, string accessTokenJti);

    /// <summary>
    /// Change password for a user.
    /// </summary>
    Task ChangePasswordAsync(int userId, ChangePasswordRequest request);
}
