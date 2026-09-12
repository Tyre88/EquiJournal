using Equine.Infrastructure.Tenancy;
using Equine.Infrastructure.Widget;

namespace Equine.Api.Middleware;

public sealed class WidgetCspMiddleware
{
    private readonly RequestDelegate _next;

    public WidgetCspMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, WidgetSettingsService settings, ITenantContext tenant)
    {
        if (context.Request.Path.StartsWithSegments("/widget")
            && !context.Request.Path.StartsWithSegments("/widget/v1"))
        {
            try
            {
                if (!tenant.HasTenant)
                {
                    context.Response.Headers.ContentSecurityPolicy = "frame-ancestors 'self'";
                }
                else
                {
                    var origins = (await settings.GetAsync(context.RequestAborted)).GetAllowedOrigins();
                    var ancestors = origins.Count == 0
                        ? "'self'"
                        : "'self' " + string.Join(' ', origins);
                    context.Response.Headers.ContentSecurityPolicy = $"frame-ancestors {ancestors}";
                }
            }
            catch (Exception)
            {
                context.Response.Headers.ContentSecurityPolicy = "frame-ancestors 'none'";
            }
        }

        await _next(context);
    }
}
