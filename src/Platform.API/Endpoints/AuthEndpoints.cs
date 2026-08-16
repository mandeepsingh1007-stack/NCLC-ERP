using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Core.Auth;
using Platform.API.Services;

namespace Platform.API.Endpoints;

/// <summary>
/// Authentication endpoints: login, refresh, logout, password change.
/// Returns proper HTTP status codes: 200, 204, 401, 403.
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        // POST /api/auth/login
        group.MapPost("/login", async (
            LoginRequest request,
            [FromServices] IAuthService authService,
            [FromServices] IHttpContextAccessor httpContext) =>
        {
            try
            {
                var ipAddress = httpContext.HttpContext?.Connection?.RemoteIpAddress?.ToString();
                var userAgent = httpContext.HttpContext?.Request?.Headers["User-Agent"].ToString();
                var response = await authService.LoginAsync(request, ipAddress, userAgent);
                return Results.Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: 401,
                    extensions: new Dictionary<string, object?> { { "error", new { code = "InvalidCredentials" } } });
            }
        })
        .AllowAnonymous()
        .WithDescription("Authenticate with username/password. Returns access token + refresh token.");

        // POST /api/auth/refresh
        group.MapPost("/refresh", async (
            RefreshRequest request,
            [FromServices] IAuthService authService,
            [FromServices] IHttpContextAccessor httpContext) =>
        {
            try
            {
                var ipAddress = httpContext.HttpContext?.Connection?.RemoteIpAddress?.ToString();
                var userAgent = httpContext.HttpContext?.Request?.Headers["User-Agent"].ToString();
                var response = await authService.RefreshAsync(request, ipAddress, userAgent);
                return Results.Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: 401,
                    extensions: new Dictionary<string, object?> { { "error", new { code = "InvalidRefreshToken" } } });
            }
        })
        .AllowAnonymous()
        .WithDescription("Rotate access token using a valid refresh token.");

        // POST /api/auth/logout
        group.MapPost("/logout", async (
            [FromHeader(Name = "Authorization")] string? authHeader,
            RefreshRequest request,
            [FromServices] IAuthService authService) =>
        {
            // Extract JTI from access token (if provided)
            string? accessTokenJti = null;
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            {
                var token = authHeader.Substring(7);
                var handler = new JwtSecurityTokenHandler();
                if (handler.CanReadToken(token))
                {
                    var jwt = handler.ReadJwtToken(token);
                    accessTokenJti = jwt.Id;
                }
            }

            try
            {
                await authService.LogoutAsync(request.RefreshToken, accessTokenJti ?? string.Empty);
            }
            catch { /* Logout is idempotent — never fail */ }

            return Results.NoContent();
        })
        .AllowAnonymous()
        .WithDescription("Revoke refresh token and add access token to deny list. Idempotent.");

        // POST /api/auth/change-password
        group.MapPost("/change-password", async (
            ChangePasswordRequest request,
            ClaimsPrincipal user,
            [FromServices] IAuthService authService) =>
        {
            var userIdStr = user.FindFirst(AuthClaimTypes.UserId)?.Value;
            if (userIdStr == null || !int.TryParse(userIdStr, out var userId))
                return Results.Unauthorized();

            try
            {
                await authService.ChangePasswordAsync(userId, request);
                return Results.Ok(new { message = "Password changed successfully." });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = new { code = "ChangePasswordFailed", message = ex.Message } });
            }
        })
        .RequireAuthorization()
        .WithDescription("Change password for the authenticated user.");

        return app;
    }
}
