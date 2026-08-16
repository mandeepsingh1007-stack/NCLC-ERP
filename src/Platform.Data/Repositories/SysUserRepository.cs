using Dapper;
using Platform.Core.Auth;
using Npgsql;

namespace Platform.Data.Repositories;

/// <summary>
/// Dapper repository for user authentication operations.
/// </summary>
public class SysUserRepository : IUserRepository
{
    private readonly string _connectionString;

    public SysUserRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<(int UserId, string Username, string PasswordHash, string Name, int ClientId, int? OrgId, bool IsActive, int FailedLoginAttempts, DateTime? LockedUntil)?> GetUserByUsernameAsync(string username)
    {
        var sql = @"SELECT ""SysUser_ID"", ""Username"", ""PasswordHash"", ""Name"", ""SysClient_ID"", ""SysOrg_ID"", ""IsActive"", ""FailedLoginAttempts"", ""LockedUntil""
                    FROM ""SysUser""
                    WHERE ""Username"" = @Username AND ""IsActive"" = true";

        using var conn = new NpgsqlConnection(_connectionString);
        var user = await conn.QueryFirstOrDefaultAsync<(int UserId, string Username, string PasswordHash, string Name, int ClientId, int? OrgId, bool IsActive, int FailedLoginAttempts, DateTime? LockedUntil)>(
            sql, new { Username = username });

        return user.IsActive ? user : null;
    }

    public async Task<(int UserId, string Username, string PasswordHash, string Name, int ClientId, int? OrgId, bool IsActive)?> GetUserByIdAsync(int userId)
    {
        var sql = @"SELECT ""SysUser_ID"", ""Username"", ""PasswordHash"", ""Name"", ""SysClient_ID"", ""SysOrg_ID"", ""IsActive""
                    FROM ""SysUser""
                    WHERE ""SysUser_ID"" = @UserId AND ""IsActive"" = true";

        using var conn = new NpgsqlConnection(_connectionString);
        var user = await conn.QueryFirstOrDefaultAsync<(int UserId, string Username, string PasswordHash, string Name, int ClientId, int? OrgId, bool IsActive)>(
            sql, new { UserId = userId });

        return user.IsActive ? user : null;
    }

    public async Task UpdateLoginAttemptsAsync(int userId, int failedAttempts, DateTime? lockedUntil)
    {
        var sql = @"UPDATE ""SysUser"" SET ""FailedLoginAttempts"" = @FailedAttempts, ""LockedUntil"" = @LockedUntil WHERE ""SysUser_ID"" = @UserId";

        using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(sql, new { UserId = userId, FailedAttempts = failedAttempts, LockedUntil = lockedUntil });
    }

    public async Task ResetLoginAttemptsAsync(int userId)
    {
        var sql = @"UPDATE ""SysUser"" SET ""FailedLoginAttempts"" = 0, ""LockedUntil"" = NULL WHERE ""SysUser_ID"" = @UserId";

        using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(sql, new { UserId = userId });
    }

    public async Task UpdatePasswordAsync(int userId, string passwordHash)
    {
        var sql = @"UPDATE ""SysUser"" SET ""PasswordHash"" = @Hash, ""UpdatedAt"" = NOW() WHERE ""SysUser_ID"" = @UserId";

        using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(sql, new { UserId = userId, Hash = passwordHash });
    }

    public async Task<IEnumerable<int>> GetUserRoleIdsAsync(int userId)
    {
        var sql = @"SELECT ""SysRole_ID"" FROM ""SysUserRoles"" WHERE ""SysUser_ID"" = @UserId";

        using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QueryAsync<int>(sql, new { UserId = userId });
    }

    public async Task<IEnumerable<(long SessionId, string AccessTokenJti, DateTime CreatedAt, string? IpAddress)>> GetUserSessionsAsync(int userId)
    {
        var sql = @"SELECT ""SysSession_ID"", ""AccessTokenJti"", ""CreatedAt"", ""IpAddress""
                    FROM ""SysSession""
                    WHERE ""SysUser_ID"" = @UserId AND ""IsRevoked"" = false
                    ORDER BY ""CreatedAt"" DESC";

        using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QueryAsync<(long SessionId, string AccessTokenJti, DateTime CreatedAt, string? IpAddress)>(
            sql, new { UserId = userId });
    }

    public async Task<long> CreateSessionAsync(int userId, string refreshTokenHash, string accessTokenJti, string? ipAddress, string? userAgent, DateTime expiresAt)
    {
        var sql = @"INSERT INTO ""SysSession"" (""SysUser_ID"", ""RefreshTokenHash"", ""AccessTokenJti"", ""IpAddress"", ""UserAgent"", ""ExpiresAt"")
                    VALUES (@UserId, @RefreshTokenHash, @AccessTokenJti, @IpAddress, @UserAgent, @ExpiresAt)
                    RETURNING ""SysSession_ID""";

        using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QuerySingleAsync<long>(sql, new
        {
            UserId = userId,
            RefreshTokenHash = refreshTokenHash,
            AccessTokenJti = accessTokenJti,
            IpAddress = (object?)ipAddress ?? DBNull.Value,
            UserAgent = (object?)userAgent ?? DBNull.Value,
            ExpiresAt = expiresAt
        });
    }

    public async Task<(long SessionId, int UserId, string AccessTokenJti, DateTime ExpiresAt)?> FindSessionByRefreshTokenHashAsync(string refreshTokenHash)
    {
        var sql = @"SELECT ""SysSession_ID"", ""SysUser_ID"", ""AccessTokenJti"", ""ExpiresAt""
                    FROM ""SysSession""
                    WHERE ""RefreshTokenHash"" = @Hash AND ""IsRevoked"" = false
                    AND ""ExpiresAt"" > NOW()
                    ORDER BY ""CreatedAt"" DESC
                    LIMIT 1";

        using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QueryFirstOrDefaultAsync<(long SessionId, int UserId, string AccessTokenJti, DateTime ExpiresAt)>(
            sql, new { Hash = refreshTokenHash });
    }

    public async Task RevokeSessionAsync(long sessionId)
    {
        var sql = @"UPDATE ""SysSession"" SET ""IsRevoked"" = true, ""RevokedAt"" = NOW() WHERE ""SysSession_ID"" = @SessionId";

        using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(sql, new { SessionId = sessionId });
    }

    public async Task<int> DeleteExpiredSessionsAsync(DateTime before)
    {
        var sql = @"DELETE FROM ""SysSession"" WHERE ""IsRevoked"" = true AND ""CreatedAt"" < @Before";

        using var conn = new NpgsqlConnection(_connectionString);
        return await conn.ExecuteAsync(sql, new { Before = before });
    }
}
