namespace Platform.Core.Auth;

/// <summary>
/// Data access for authentication and user management.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Look up a user by username. Returns null if not found.
    /// </summary>
    Task<(int UserId, string Username, string PasswordHash, string Name, int ClientId, int? OrgId, bool IsActive, int FailedLoginAttempts, DateTime? LockedUntil)?> GetUserByUsernameAsync(string username);

    /// <summary>
    /// Look up a user by ID. Returns null if not found.
    /// </summary>
    Task<(int UserId, string Username, string PasswordHash, string Name, int ClientId, int? OrgId, bool IsActive)?> GetUserByIdAsync(int userId);

    /// <summary>
    /// Update failed login attempts and lock time for a user.
    /// </summary>
    Task UpdateLoginAttemptsAsync(int userId, int failedAttempts, DateTime? lockedUntil);

    /// <summary>
    /// Reset failed login attempts on successful login.
    /// </summary>
    Task ResetLoginAttemptsAsync(int userId);

    /// <summary>
    /// Update password hash for a user.
    /// </summary>
    Task UpdatePasswordAsync(int userId, string passwordHash);

    /// <summary>
    /// Get the role IDs for a given user.
    /// </summary>
    Task<IEnumerable<int>> GetUserRoleIdsAsync(int userId);

    /// <summary>
    /// Get all active sessions for a user (for concurrent session limit enforcement).
    /// </summary>
    Task<IEnumerable<(long SessionId, string AccessTokenJti, DateTime CreatedAt, string? IpAddress)>> GetUserSessionsAsync(int userId);

    /// <summary>
    /// Create a new session record.
    /// </summary>
    Task<long> CreateSessionAsync(int userId, string refreshTokenHash, string accessTokenJti, string? ipAddress, string? userAgent, DateTime expiresAt);

    /// <summary>
    /// Find a session by its refresh token hash. Returns null if not found or revoked.
    /// </summary>
    Task<(long SessionId, int UserId, string AccessTokenJti, DateTime ExpiresAt)?> FindSessionByRefreshTokenHashAsync(string refreshTokenHash);

    /// <summary>
    /// Revoke a session (mark as revoked).
    /// </summary>
    Task RevokeSessionAsync(long sessionId);

    /// <summary>
    /// Delete expired sessions older than the given date.
    /// </summary>
    Task<int> DeleteExpiredSessionsAsync(DateTime before);
}
