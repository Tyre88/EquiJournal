namespace Equine.Api.Middleware;

public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

        var path = context.Request.Path;
        if (!path.StartsWithSegments("/widget") && !path.StartsWithSegments("/api/public"))
        {
            headers.ContentSecurityPolicy =
                "default-src 'self'; " +
                "script-src 'self' https://maps.googleapis.com https://maps.gstatic.com; " +
                "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
                "img-src 'self' data: blob: https://*.googleapis.com https://*.gstatic.com https://*.ggpht.com; " +
                "font-src 'self' https://fonts.gstatic.com; " +
                "connect-src 'self' https://maps.googleapis.com https://maps.gstatic.com; " +
                "frame-ancestors 'none'; " +
                "base-uri 'self'; " +
                "form-action 'self'";
        }

        return _next(context);
    }
}
