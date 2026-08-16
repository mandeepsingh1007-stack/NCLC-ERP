using System.Security.Claims;
using BCrypt.Net;
using Platform.Core.Auth;

namespace Platform.API.Services;

/// <summary>
/// Orchestrates authentication: login, refresh, logout.
/// Uses BCrypt for password hashing, ITokenService for JWT/refresh tokens,
/// and IUserRepository for user/session persistence.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepo;
    private readonly ITokenService _tokenService;
    private readonly JwtSettings _jwtSettings;
    private readonly IHttpContextAccessor _httpContext;
    private const int MaxFailedAttempts = 5;

    public AuthService(IUserRepository userRepo, ITokenService tokenService, IConfiguration configuration, IHttpContextAccessor httpContext)
    {
        _userRepo = userRepo;
        _tokenService = tokenService;
        _httpContext = httpContext;
        _jwtSettings = configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
    }

    private string? GetIpAddress()
        => _httpContext.HttpContext?.Connection?.RemoteIpAddress?.ToString();

    private string? GetUserAgent()
        => _httpContext.HttpContext?.Request?.Headers["User-Agent"].ToString();

    public async Task<LoginResponse> LoginAsync(LoginRequest request, string? ipAddress = null, string? userAgent = null)
    {
        ipAddress = ipAddress ?? GetIpAddress();
        userAgent = userAgent ?? GetUserAgent();

        var user = await _userRepo.GetUserByUsernameAsync(request.Username);
        if (user == null)
            throw new InvalidOperationException("Invalid username or password.");

        if (user.Value.LockedUntil.HasValue && user.Value.LockedUntil.Value > DateTime.UtcNow)
            throw new InvalidOperationException($"Account is locked until {user.Value.LockedUntil.Value:yyyy-MM-dd HH:mm:ss}.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.Value.PasswordHash))
        {
            var failedAttempts = user.Value.FailedLoginAttempts + 1;
            var lockedUntil = (DateTime?)(failedAttempts >= MaxFailedAttempts
                ? DateTime.UtcNow.AddHours(1)
                : (DateTime?)null);
            await _userRepo.UpdateLoginAttemptsAsync(user.Value.UserId, failedAttempts, lockedUntil);
            throw new InvalidOperationException("Invalid username or password.");
        }

        await _userRepo.ResetLoginAttemptsAsync(user.Value.UserId);

        var roleIds = await _userRepo.GetUserRoleIdsAsync(user.Value.UserId);

        // Enforce concurrent session limit
        var sessions = await _userRepo.GetUserSessionsAsync(user.Value.UserId);
        int activeCount = sessions.Count();
        if (activeCount >= _jwtSettings.MaxConcurrentSessions)
        {
            var toRevoke = sessions.OrderBy(s => s.CreatedAt).Take(activeCount - _jwtSettings.MaxConcurrentSessions + 1);
            foreach (var s in toRevoke)
                await _userRepo.RevokeSessionAsync(s.SessionId);
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(AuthClaimTypes.UserId, user.Value.UserId.ToString()),
            new Claim(AuthClaimTypes.Username, user.Value.Username),
            new Claim(AuthClaimTypes.ClientId, user.Value.ClientId.ToString()),
        }));

        var tokenResult = _tokenService.Generate(principal, roleIds.Select(r => r.ToString()));

        var refreshTokenHash = _tokenService.HashRefreshToken(tokenResult.RefreshToken);
        await _userRepo.CreateSessionAsync(
            user.Value.UserId,
            refreshTokenHash,
            tokenResult.AccessTokenJti,
            ipAddress,
            userAgent,
            tokenResult.ExpiresAt);

        return new LoginResponse(
            tokenResult.AccessToken,
            tokenResult.RefreshToken,
            tokenResult.ExpiresAt,
            user.Value.Username);
    }

    public async Task<LoginResponse> RefreshAsync(RefreshRequest request, string? ipAddress = null, string? userAgent = null)
    {
        var refreshTokenHash = _tokenService.HashRefreshToken(request.RefreshToken);

        var session = await _userRepo.FindSessionByRefreshTokenHashAsync(refreshTokenHash);
        if (session == null)
            throw new InvalidOperationException("Invalid or expired refresh token.");

        // Look up the user (session doesn't store full user details)
        var user = await _userRepo.GetUserByIdAsync(session.Value.UserId);
        if (user == null || !user.Value.IsActive)
            throw new InvalidOperationException("User not found or inactive.");

        var roleIds = await _userRepo.GetUserRoleIdsAsync(user.Value.UserId);

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(AuthClaimTypes.UserId, user.Value.UserId.ToString()),
            new Claim(AuthClaimTypes.Username, user.Value.Username),
            new Claim(AuthClaimTypes.ClientId, user.Value.ClientId.ToString()),
        }));

        var tokenResult = _tokenService.Generate(principal, roleIds.Select(r => r.ToString()));

        // Revoke old session and create new one (token rotation)
        await _userRepo.RevokeSessionAsync(session.Value.SessionId);
        await _tokenService.AddToDenyListAsync(session.Value.AccessTokenJti);

        var newRefreshHash = _tokenService.HashRefreshToken(tokenResult.RefreshToken);
        await _userRepo.CreateSessionAsync(
            user.Value.UserId,
            newRefreshHash,
            tokenResult.AccessTokenJti,
            ipAddress ?? GetIpAddress(),
            userAgent ?? GetUserAgent(),
            tokenResult.ExpiresAt);

        return new LoginResponse(
            tokenResult.AccessToken,
            tokenResult.RefreshToken,
            tokenResult.ExpiresAt,
            user.Value.Username);
    }

    public async Task LogoutAsync(string refreshToken, string accessTokenJti)
    {
        // Add access token to deny list
        if (!string.IsNullOrEmpty(accessTokenJti))
            await _tokenService.AddToDenyListAsync(accessTokenJti);

        // Find and revoke the session by refresh token
        var refreshTokenHash = _tokenService.HashRefreshToken(refreshToken);
        var session = await _userRepo.FindSessionByRefreshTokenHashAsync(refreshTokenHash);
        if (session != null)
            await _userRepo.RevokeSessionAsync(session.Value.SessionId);
    }

    public async Task ChangePasswordAsync(int userId, ChangePasswordRequest request)
    {
        var user = await _userRepo.GetUserByIdAsync(userId);
        if (user == null)
            throw new InvalidOperationException("User not found.");

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.Value.PasswordHash))
            throw new InvalidOperationException("Current password is incorrect.");

        var newHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _userRepo.UpdatePasswordAsync(userId, newHash);
    }
}
