using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Equine.Infrastructure;
using Equine.Infrastructure.Audit;
using Equine.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using System.Security.Cryptography;
using Serilog;

namespace Equine.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/app/auth");

        group.MapPost("/login", async (
            [FromBody] LoginRequest request,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration,
            IAuditWriter auditWriter,
            CancellationToken ct) =>
        {
            var user = await userManager.FindByEmailAsync(request.Email)
                ?? await userManager.FindByNameAsync(request.Email);
            if (user is null) return Results.Json(new { error = "invalid_credentials" });

            var result = await signInManager.PasswordSignInAsync(user.UserName!, request.Password, false, true);
            if (!result.Succeeded) return Results.Json(new { error = "invalid_credentials" });

            Log.Information("Login successful for user {UserId} ({Email})", user.Id, user.Email);

            var tokens = await IssueTokens(user, userManager, configuration);
            Log.Information("Generated tokens for user {UserId} - token length: {Length}", user.Id, tokens.AccessToken.Length);
            await auditWriter.WriteAsync(user.Id.ToString(), "LOGIN", "ApplicationUser", user.Id.ToString(), null, null, ct);

            return Results.Ok(new
            {
                accessToken = tokens.AccessToken,
                refreshToken = tokens.RefreshToken,
                expires = tokens.Expires
            });
        }).WithName("Login");

        group.MapPost("/refresh", async (
            [FromBody] RefreshTokenRequest request,
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.AccessToken) || string.IsNullOrWhiteSpace(request.RefreshToken))
                return Results.Json(new { error = "invalid_token" }, statusCode: StatusCodes.Status401Unauthorized);

            var principal = GetPrincipalFromExpiredToken(request.AccessToken, configuration);
            if (principal is null)
                return Results.Json(new { error = "invalid_token" }, statusCode: StatusCodes.Status401Unauthorized);

            var email = principal.FindFirstValue(ClaimTypes.Email);
            var user = email is not null ? await userManager.FindByEmailAsync(email) : null;
            if (user is null || !RefreshTokenMatches(user, request.RefreshToken))
                return Results.Json(new { error = "invalid_token" }, statusCode: StatusCodes.Status401Unauthorized);

            var tokens = await IssueTokens(user, userManager, configuration);
            return Results.Ok(new
            {
                accessToken = tokens.AccessToken,
                refreshToken = tokens.RefreshToken,
                expires = tokens.Expires
            });
        }).WithName("RefreshToken");

        group.MapPost("/logout", async (
            HttpContext context,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IAuditWriter auditWriter, CancellationToken ct) =>
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is not null)
            {
                var user = await userManager.FindByIdAsync(userId);
                if (user is not null)
                {
                    ClearRefreshToken(user);
                    await userManager.UpdateAsync(user);
                }
                await auditWriter.WriteAsync(userId, "LOGOUT", "ApplicationUser", userId, null, null, ct);
            }
            await signInManager.SignOutAsync();
            return Results.Ok();
        }).RequireAuthorization().WithName("Logout");

        group.MapPost("/change-password", async (
            ChangePasswordRequest request,
            HttpContext context,
            UserManager<ApplicationUser> userManager,
            IAuditWriter auditWriter,
            CancellationToken ct) =>
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Results.Unauthorized();
            var user = await userManager.FindByIdAsync(userId);
            if (user is null) return Results.Unauthorized();
            var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded)
                return Results.BadRequest(new { error = string.Join(" ", result.Errors.Select(e => e.Description)) });
            ClearRefreshToken(user);
            await userManager.UpdateAsync(user);
            await auditWriter.WriteAsync(userId, "PASSWORD_CHANGE", "ApplicationUser", userId, null, null, ct);
            return Results.Ok();
        }).RequireAuthorization().WithName("ChangePassword");

        return app;
    }

    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    public static Task<(string AccessToken, string RefreshToken, DateTimeOffset Expires)> IssueTokensForUser(
        ApplicationUser user, UserManager<ApplicationUser> userManager, IConfiguration configuration) =>
        IssueTokens(user, userManager, configuration);

    private static async Task<(string AccessToken, string RefreshToken, DateTimeOffset Expires)> IssueTokens(
        ApplicationUser user, UserManager<ApplicationUser> userManager, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("Jwt");
        var securityKey = jwtSettings["SecurityKey"]!;
        var key = Encoding.ASCII.GetBytes(securityKey);
        var accessMinutes = jwtSettings.GetValue<int>("AccessTokenExpiryMinutes", 15);
        var refreshHours = jwtSettings.GetValue<int>("RefreshTokenExpiryHours", 4);
        var expires = DateTimeOffset.UtcNow.AddMinutes(accessMinutes);

        var roles = await userManager.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new("displayName", user.DisplayName ?? user.UserName ?? string.Empty),
            new("sub", user.Id.ToString()),
            new("isFirstLogin", user.IsFirstLogin.ToString()),
            new("tenantId", user.TenantId.ToString())
        };
        foreach (var role in roles)
        {
            claims.Add(new Claim("role", role));
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires.UtcDateTime,
            Issuer = jwtSettings["Issuer"],
            Audience = jwtSettings["Audience"],
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var accessToken = tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        user.RefreshTokenHash = HashRefreshToken(refreshToken);
        user.RefreshTokenExpiresAt = DateTimeOffset.UtcNow.AddHours(refreshHours);
        await userManager.UpdateAsync(user);

        return (accessToken, refreshToken, expires);
    }

    private static bool RefreshTokenMatches(ApplicationUser user, string refreshToken)
    {
        if (string.IsNullOrEmpty(user.RefreshTokenHash)
            || user.RefreshTokenExpiresAt is not DateTimeOffset expiry
            || expiry <= DateTimeOffset.UtcNow)
            return false;

        var incoming = HashRefreshToken(refreshToken);
        var stored = Encoding.UTF8.GetBytes(user.RefreshTokenHash);
        var offered = Encoding.UTF8.GetBytes(incoming);
        return stored.Length == offered.Length
               && CryptographicOperations.FixedTimeEquals(stored, offered);
    }

    private static void ClearRefreshToken(ApplicationUser user)
    {
        user.RefreshTokenHash = null;
        user.RefreshTokenExpiresAt = null;
    }

    private static string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    private static ClaimsPrincipal? GetPrincipalFromExpiredToken(string token, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("Jwt");
        var securityKey = jwtSettings["SecurityKey"]!;
        var key = System.Text.Encoding.ASCII.GetBytes(securityKey);

        var tokenHandler = new JwtSecurityTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = false,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"], ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };

        try { return tokenHandler.ValidateToken(token, parameters, out _); }
        catch { return null; }
    }
}
