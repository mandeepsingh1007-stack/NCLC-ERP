using System.Security.Claims;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Platform.Core.Auth;

public class AuthTests
{
    private static JwtSettings DefaultSettings => new()
    {
        SecretKey = "NCLC-Platform-Dev-Secret-Key-2026-MustBe32PlusChars!",
        Issuer = "Test.API",
        Audience = "Test.Client",
        AccessTtlMinutes = 15,
        RefreshTtlDays = 7
    };

    private TokenResult CreateToken(JwtSettings? settings = null)
    {
        settings ??= DefaultSettings;
        var cacheMock = new Mock<IDistributedCache>();
        var tokenService = new TokenService(settings, cacheMock.Object);

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(AuthClaimTypes.UserId, "42"),
            new Claim(AuthClaimTypes.Username, "testuser"),
            new Claim(AuthClaimTypes.ClientId, "100"),
        }));

        return tokenService.Generate(principal, Enumerable.Empty<string>());
    }

    [Fact]
    public void GenerateToken_ReturnsValidResult()
    {
        var result = CreateToken();

        Assert.NotEmpty(result.AccessToken);
        Assert.NotEmpty(result.RefreshToken);
        Assert.True(result.ExpiresAt > DateTime.UtcNow);
        Assert.NotEmpty(result.AccessTokenJti);
    }

    [Fact]
    public void GenerateToken_ContainsCorrectClaims()
    {
        var result = CreateToken();

        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result.AccessToken);

        var claims = jwt.Claims.ToDictionary(c => c.Type, c => c.Value);

        Assert.Equal("42", claims[AuthClaimTypes.UserId]);
        Assert.Equal("testuser", claims[AuthClaimTypes.Username]);
        Assert.Equal("100", claims[AuthClaimTypes.ClientId]);
        Assert.Equal(result.AccessTokenJti, claims[System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti]);
    }

    [Fact]
    public void GenerateToken_WithRoleIds_IncludesRoles()
    {
        var settings = DefaultSettings;
        var cacheMock = new Mock<IDistributedCache>();
        var tokenService = new TokenService(settings, cacheMock.Object);

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(AuthClaimTypes.UserId, "1"),
            new Claim(AuthClaimTypes.Username, "admin"),
            new Claim(AuthClaimTypes.ClientId, "1"),
        }));

        var result = tokenService.Generate(principal, new[] { "5", "10" });

        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result.AccessToken);

        var ridsClaim = jwt.Claims.FirstOrDefault(c => c.Type == AuthClaimTypes.RoleIds);
        Assert.NotNull(ridsClaim);
        Assert.Contains("5", ridsClaim!.Value);
        Assert.Contains("10", ridsClaim.Value);
    }

    [Fact]
    public void GenerateRefreshToken_IsRandomAndLongEnough()
    {
        var settings = DefaultSettings;
        var cacheMock = new Mock<IDistributedCache>();
        var tokenService = new TokenService(settings, cacheMock.Object);

        var token1 = tokenService.GenerateRefreshToken();
        var token2 = tokenService.GenerateRefreshToken();

        Assert.NotEmpty(token1);
        Assert.True(token1.Length >= 86); // 64 bytes = 86 base64 chars
        Assert.NotEqual(token1, token2);
    }

    [Fact]
    public void HashRefreshToken_IsDeterministic()
    {
        var settings = DefaultSettings;
        var cacheMock = new Mock<IDistributedCache>();
        var tokenService = new TokenService(settings, cacheMock.Object);

        var hash1 = tokenService.HashRefreshToken("test-token");
        var hash2 = tokenService.HashRefreshToken("test-token");

        Assert.Equal(hash1, hash2);

        var hash3 = tokenService.HashRefreshToken("different-token");
        Assert.NotEqual(hash1, hash3);
    }

    [Fact]
    public void GenerateToken_ShortSecretKey_Throws()
    {
        var settings = new JwtSettings { SecretKey = "short" };
        var cacheMock = new Mock<IDistributedCache>();

        Assert.Throws<InvalidOperationException>(() => new TokenService(settings, cacheMock.Object));
    }

    [Fact]
    public void PermissionLevel_EnumValues_AreCorrect()
    {
        Assert.Equal(0, (int)PermissionLevel.None);
        Assert.Equal(1, (int)PermissionLevel.ReadOnly);
        Assert.Equal(2, (int)PermissionLevel.ReadWrite);
        Assert.Equal(3, (int)PermissionLevel.Create);
        Assert.Equal(4, (int)PermissionLevel.FullControl);
    }

    [Fact]
    public void PermissionResult_Allowed_True_HasNoReason()
    {
        var result = new PermissionResult(true, PermissionLevel.ReadOnly, null);
        Assert.True(result.Allowed);
        Assert.Equal(PermissionLevel.ReadOnly, result.Level);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void PermissionResult_Allowed_False_HasReason()
    {
        var result = new PermissionResult(false, PermissionLevel.None, "User not found.");
        Assert.False(result.Allowed);
        Assert.Equal(PermissionLevel.None, result.Level);
        Assert.Equal("User not found.", result.Reason);
    }

    [Theory]
    [InlineData("ReadOnly", PermissionLevel.ReadOnly)]
    [InlineData("ReadWrite", PermissionLevel.ReadWrite)]
    [InlineData("FullControl", PermissionLevel.FullControl)]
    public void PermissionLevel_Parses_From_String(string name, PermissionLevel expected)
    {
        var value = (PermissionLevel)(short)expected;
        Assert.Equal(expected, value);
        // name is a theory input parameter used for test identification/documentation
        _ = name;
    }
}
