namespace Platform.Core.Auth;

/// <summary>
/// JWT and session configuration from appsettings.json.
/// </summary>
public class JwtSettings
{
    /// <summary>
    /// Secret key for signing JWT tokens (must be set via environment variables).
    /// Minimum 32 characters recommended.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Issuer name embedded in access tokens.
    /// </summary>
    public string Issuer { get; set; } = "Platform.API";

    /// <summary>
    /// Audience name embedded in access tokens.
    /// </summary>
    public string Audience { get; set; } = "Platform.Client";

    /// <summary>
    /// Access token TTL in minutes (default 15).
    /// </summary>
    public int AccessTtlMinutes { get; set; } = 15;

    /// <summary>
    /// Refresh token TTL in days (default 7).
    /// </summary>
    public int RefreshTtlDays { get; set; } = 7;

    /// <summary>
    /// Maximum concurrent sessions per user (default 5).
    /// </summary>
    public int MaxConcurrentSessions { get; set; } = 5;
}
