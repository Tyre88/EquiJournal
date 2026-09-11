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

            var (accessToken, refreshToken) = await GenerateTokens(user, userManager, configuration);
            Log.Information("Generated tokens for user {UserId} - token length: {Length}", user.Id, accessToken?.Length);
            await auditWriter.WriteAsync(user.Id.ToString(), "LOGIN", "ApplicationUser", user.Id.ToString(), null, null, ct);

            return Results.Ok(new
            {
                accessToken, refreshToken,
                expires = DateTimeOffset.UtcNow.AddMinutes(configuration.GetValue<int>("Jwt:AccessTokenExpiryMinutes", 15)),
                requiresTwoFactor = false
            });
        }).WithName("Login");

        group.MapPost("/refresh", async (
            [FromBody] RefreshTokenRequest request,
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration, CancellationToken ct) =>
        {
            var principal = GetPrincipalFromExpiredToken(request.AccessToken, configuration);
            if (principal is null) return Results.Json(new { error = "invalid_token" });

            var email = principal.FindFirstValue(ClaimTypes.Email);
            var user = email is not null ? await userManager.FindByEmailAsync(email) : null;
            if (user is null) return Results.Json(new { error = "invalid_token" });

            var (accessToken, refreshToken) = await GenerateTokens(user, userManager, configuration);
            return Results.Ok(new
            {
                accessToken, refreshToken,
                expires = DateTimeOffset.UtcNow.AddMinutes(configuration.GetValue<int>("Jwt:AccessTokenExpiryMinutes", 15))
            });
        }).WithName("RefreshToken");

        group.MapPost("/enrol-2fa", async (
            HttpContext context,
            UserManager<ApplicationUser> userManager,
            ITotpService totpService,
            IAuditWriter auditWriter, CancellationToken ct) =>
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Results.Unauthorized();
            var user = await userManager.FindByIdAsync(userId);
            if (user is null) return Results.NotFound(new { error = "user_not_found" });

            var provisioningUri = totpService.GenerateProvisioningUri(user);
            await userManager.UpdateAsync(user);
            await auditWriter.WriteAsync(userId, "ENROL_2FA", "ApplicationUser", userId, null, null, ct);
            return Results.Ok(new EnrolTotpResponse(provisioningUri, user.TotpSecret ?? ""));
        }).RequireAuthorization().WithName("Enrol2FA");

        group.MapPost("/verify-2fa", async (
            [FromBody] VerifyTotpRequest request,
            HttpContext context,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IConfiguration configuration,
            ITotpService totpService,
            IAuditWriter auditWriter, CancellationToken ct) =>
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Results.Unauthorized();
            var user = await userManager.FindByIdAsync(userId);
            if (user is null) return Results.NotFound(new { error = "user_not_found" });
            if (string.IsNullOrEmpty(user.TotpSecret))
                return Results.BadRequest(new { error = "no_secret_configured" });

            var isValid = totpService.VerifyTotp(user.TotpSecret, request.Code);
            if (!isValid) return Results.BadRequest(new { error = "invalid_code" });

            user.TwoFactorEnabled = true;
            user.TotpSecret = null;
            user.IsFirstLogin = false;
            await userManager.UpdateAsync(user);
            await signInManager.SignInAsync(user, isPersistent: false);

            var (accessToken, refreshToken) = await GenerateTokens(user, userManager, configuration);
            await auditWriter.WriteAsync(userId, "VERIFY_2FA", "ApplicationUser", userId, null, null, ct);

            return Results.Ok(new
            {
                accessToken, refreshToken,
                expires = DateTimeOffset.UtcNow.AddMinutes(configuration.GetValue<int>("Jwt:AccessTokenExpiryMinutes", 15))
            });
        }).RequireAuthorization().WithName("Verify2FA");

        group.MapPost("/logout", async (
            HttpContext context,
            SignInManager<ApplicationUser> signInManager,
            IAuditWriter auditWriter, CancellationToken ct) =>
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is not null)
                await auditWriter.WriteAsync(userId, "LOGOUT", "ApplicationUser", userId, null, null, ct);
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
            await auditWriter.WriteAsync(userId, "PASSWORD_CHANGE", "ApplicationUser", userId, null, null, ct);
            return Results.Ok();
        }).RequireAuthorization().WithName("ChangePassword");

        return app;
    }

    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    private static async Task<(string AccessToken, string RefreshToken)> GenerateTokens(
        ApplicationUser user, UserManager<ApplicationUser> userManager, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("Jwt");
        var securityKey = jwtSettings["SecurityKey"]!;
        var key = System.Text.Encoding.ASCII.GetBytes(securityKey);

        var roles = await userManager.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new("displayName", user.DisplayName ?? user.UserName ?? string.Empty),
            new("sub", user.Id.ToString()),
            new("isFirstLogin", user.IsFirstLogin.ToString()),
            new("twoFactorEnabled", user.TwoFactorEnabled.ToString())
        };
        foreach (var role in roles)
        {
            claims.Add(new Claim("role", role));
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTimeOffset.UtcNow.AddMinutes(jwtSettings.GetValue<int>("AccessTokenExpiryMinutes", 15)).UtcDateTime,
            Issuer = jwtSettings["Issuer"],
            Audience = jwtSettings["Audience"],
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var accessToken = tokenHandler.WriteToken(token);
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        return (accessToken, refreshToken);
    }

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
