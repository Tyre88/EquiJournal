using Equine.Infrastructure.Widget;
using Microsoft.AspNetCore.Cors.Infrastructure;

namespace Equine.Api.Cors;

public sealed class DynamicCorsPolicyProvider : ICorsPolicyProvider
{
    public const string PublicPolicy = "Public";
    public const string AppPolicy = "App";

    private readonly IServiceScopeFactory _scopes;
    private readonly IHostEnvironment _env;
    private readonly IConfiguration _config;

    public DynamicCorsPolicyProvider(IServiceScopeFactory scopes, IHostEnvironment env, IConfiguration config)
    {
        _scopes = scopes;
        _env = env;
        _config = config;
    }

    public async Task<CorsPolicy?> GetPolicyAsync(HttpContext context, string? policyName)
    {
        var origins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var configured = _config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        foreach (var origin in configured)
            origins.Add(origin);

        if (_env.IsDevelopment())
        {
            origins.Add("http://localhost:4200");
            origins.Add("http://127.0.0.1:4200");
            origins.Add("http://localhost:4201");
            origins.Add("http://127.0.0.1:4201");
            origins.Add("http://localhost:4210");
            origins.Add("http://127.0.0.1:4210");
        }

        if (string.Equals(policyName, PublicPolicy, StringComparison.OrdinalIgnoreCase)
            || context.Request.Path.StartsWithSegments("/api/public"))
        {
            using var scope = _scopes.CreateScope();
            var settings = await scope.ServiceProvider.GetRequiredService<WidgetSettingsService>().GetAsync(context.RequestAborted);
            foreach (var origin in settings.GetAllowedOrigins())
                origins.Add(origin);
        }

        var builder = new CorsPolicyBuilder();
        builder.WithOrigins(origins.Where(o => !o.Contains('*')).ToArray())
            .AllowAnyHeader()
            .AllowAnyMethod();
        return builder.Build();
    }
}
