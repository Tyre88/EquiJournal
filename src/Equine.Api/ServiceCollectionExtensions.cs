using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Equine.Api.Auth;
using Equine.Infrastructure;
using Serilog;

namespace Equine.Api;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("Jwt");
        var securityKey = jwtSettings["SecurityKey"]
            ?? throw new InvalidOperationException("JWT SecurityKey not configured.");

        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 12;
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<EquineDbContext>()
        .AddDefaultTokenProviders();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.SaveToken = true;
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidAudience = jwtSettings["Audience"],
                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.ASCII.GetBytes(securityKey)),
                ClockSkew = TimeSpan.FromSeconds(30),
                RoleClaimType = ClaimTypes.Role,
                NameClaimType = ClaimTypes.NameIdentifier
            };

            options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var authHeader = context.Request.Headers["Authorization"].ToString();
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(authHeader))
                        Log.Debug("Auth header received: {Header}", authHeader);
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/signalr"))
                        context.Token = accessToken;
                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    Log.Warning("JWT auth challenge: {Scheme}", context.Scheme.Name);
                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorizationBuilder()
            .AddPolicy("CanReadJournals", policy =>
                policy.RequireAssertion(ctx => ctx.User.IsInRole("Admin") || ctx.User.IsInRole("Practitioner")))
            .AddPolicy("CanWriteJournals", policy =>
                policy.RequireAssertion(ctx => ctx.User.IsInRole("Admin") || ctx.User.IsInRole("Practitioner")))
            .AddPolicy("CanManageBookings", policy =>
                policy.RequireAssertion(ctx => ctx.User.IsInRole("Admin") || ctx.User.IsInRole("Practitioner") || ctx.User.IsInRole("Assistant")))
            .AddPolicy("CanAdminister", policy => policy.RequireRole("Admin"));

        services.AddScoped<Auth.ITotpService, Auth.TotpService>();
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
            options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
        });
        services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(180);
            options.IncludeSubDomains = true;
            options.Preload = false;
        });
        return services;
    }
}
