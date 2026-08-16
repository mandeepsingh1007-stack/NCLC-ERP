using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;

namespace Platform.Core.Auth;

/// <summary>
/// Result of token generation or refresh.
/// </summary>
public record TokenResult(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    string? AccessTokenJti);

/// <summary>
/// Service for generating and validating JWT access tokens and refresh tokens.
/// Access tokens are short-lived (15 min). Refresh tokens are long-lived (7 days).
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Generate an access token + refresh token for the given claims.
    /// </summary>
    TokenResult Generate(ClaimsPrincipal user, IEnumerable<string> roleIds);

    /// <summary>
    /// Validate an access token and return the claims principal.
    /// Returns null if the token is expired, invalid, or in the deny list.
    /// </summary>
    Task<ClaimsPrincipal?> ValidateAsync(string token, HashSet<string>? denyListJtis = null);

    /// <summary>
    /// Generate a cryptographically secure random refresh token string.
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>
    /// Hash a refresh token for storage (SHA-256).
    /// The plaintext token is returned to the client; only the hash is stored.
    /// </summary>
    string HashRefreshToken(string refreshToken);

    /// <summary>
    /// Check if a JWT jti is in the deny list (revoked).
    /// </summary>
    Task<bool> IsInDenyListAsync(string jti);

    /// <summary>
    /// Add a JWT jti to the Redis deny list with a short TTL.
    /// </summary>
    Task AddToDenyListAsync(string jti);

    /// <summary>
    /// Remove a JWT jti from the deny list.
    /// </summary>
    Task RemoveFromDenyListAsync(string jti);
}

/// <summary>
/// Implementation using System.IdentityModel.Tokens.Jwt.
/// Refresh tokens are random strings hashed with SHA-256 for storage.
/// Token revocation uses Redis deny list (hashed jti with short TTL).
/// </summary>
public class TokenService : ITokenService
{
    private readonly JwtSettings _settings;
    private readonly IDistributedCache _cache;
    private readonly byte[] _key;

    public TokenService(JwtSettings settings, IDistributedCache cache)
    {
        _settings = settings;
        _cache = cache;
        _key = Encoding.UTF8.GetBytes(settings.SecretKey);

        if (_key.Length < 16)
            throw new InvalidOperationException("JWT SecretKey must be at least 16 characters long.");
    }

    public TokenResult Generate(ClaimsPrincipal user, IEnumerable<string> roleIds)
    {
        var jti = Guid.NewGuid().ToString("N");
        var refreshToken = GenerateRefreshToken();

        var claims = new List<Claim>
        {
            new(AuthClaimTypes.UserId, user.FindFirst(AuthClaimTypes.UserId)?.Value ?? string.Empty),
            new(AuthClaimTypes.Username, user.FindFirst(AuthClaimTypes.Username)?.Value ?? string.Empty),
            new(AuthClaimTypes.ClientId, user.FindFirst(AuthClaimTypes.ClientId)?.Value ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, jti),
        };

        if (user.FindFirst(AuthClaimTypes.OrgId)?.Value is string orgId && !string.IsNullOrEmpty(orgId))
            claims.Add(new Claim(AuthClaimTypes.OrgId, orgId));

        var roleIdList = roleIds.ToList();
        if (roleIdList.Count > 0)
            claims.Add(new Claim(AuthClaimTypes.RoleIds, string.Join(",", roleIdList)));

        var expiresAt = DateTime.UtcNow.AddMinutes(_settings.AccessTtlMinutes);

        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(_key),
            SecurityAlgorithms.HmacSha256Signature);

        var jwtToken = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: signingCredentials);

        var handler = new JwtSecurityTokenHandler();
        var accessToken = handler.WriteToken(jwtToken);

        return new TokenResult(accessToken, refreshToken, expiresAt, jti);
    }

    public async Task<ClaimsPrincipal?> ValidateAsync(string token, HashSet<string>? denyListJtis = null)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(_key),
                ValidateIssuer = true,
                ValidIssuer = _settings.Issuer,
                ValidateAudience = true,
                ValidAudience = _settings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var validationResult = await handler.ValidateTokenAsync(token, validationParameters);

            if (!validationResult.IsValid)
                return null;

            // Check deny list
            var jwtToken = handler.ReadJwtToken(token);
            var jti = jwtToken.Id;
            if (!string.IsNullOrEmpty(jti))
            {
                if (denyListJtis != null && denyListJtis.Contains(jti))
                    return null;

                // Also check Redis deny list
                var denied = await IsInDenyListAsync(jti);
                if (denied)
                    return null;
            }

            // Convert IDictionary claims to Claim objects
            var claimList = new List<Claim>();
            foreach (var kvp in validationResult.Claims)
            {
                claimList.Add(new Claim(kvp.Key, kvp.Value?.ToString() ?? string.Empty));
            }

            return new ClaimsPrincipal(new ClaimsIdentity(claimList, "jwt"));
        }
        catch
        {
            return null;
        }
    }

    public string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    public string HashRefreshToken(string refreshToken)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToBase64String(hash);
    }

    public async Task<bool> IsInDenyListAsync(string jti)
    {
        try
        {
            var hashed = HashRefreshToken(jti);
            var value = await _cache.GetStringAsync($"deny:{hashed}");
            return !string.IsNullOrEmpty(value);
        }
        catch
        {
            return false;
        }
    }

    public async Task AddToDenyListAsync(string jti)
    {
        try
        {
            var hashed = HashRefreshToken(jti);
            await _cache.SetStringAsync(
                $"deny:{hashed}",
                "revoked",
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_settings.AccessTtlMinutes + 1)
                });
        }
        catch
        {
            // Redis unavailable — deny list is a safety net, not a hard requirement
        }
    }

    public async Task RemoveFromDenyListAsync(string jti)
    {
        try
        {
            var hashed = HashRefreshToken(jti);
            await _cache.RemoveAsync($"deny:{hashed}");
        }
        catch { /* ignore */ }
    }
}
