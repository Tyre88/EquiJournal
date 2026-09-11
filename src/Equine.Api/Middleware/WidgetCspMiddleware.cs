using Equine.Infrastructure.Widget;

namespace Equine.Api.Middleware;

public sealed class WidgetCspMiddleware
{
    private readonly RequestDelegate _next;

    public WidgetCspMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, WidgetSettingsService settings)
    {
        if (context.Request.Path.StartsWithSegments("/widget"))
        {
            try
            {
                var origins = (await settings.GetAsync(context.RequestAborted)).GetAllowedOrigins();
                var ancestors = origins.Count == 0 ? "'none'" : string.Join(' ', origins);
                context.Response.Headers.ContentSecurityPolicy = $"frame-ancestors {ancestors}";
            }
            catch (Exception)
            {
                context.Response.Headers.ContentSecurityPolicy = "frame-ancestors 'none'";
            }
        }

        await _next(context);
    }
}
