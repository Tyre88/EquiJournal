using Equine.Domain.Entities;
using Equine.Infrastructure;
using Equine.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Equine.Api.Tenancy;

public sealed class TenantResolutionMiddleware
{
    private static readonly HashSet<string> PublicReserved =
        new(StringComparer.OrdinalIgnoreCase) { "ping", "auth", "bookings", "tenants", "unsubscribe", "webhooks" };

    private static readonly HashSet<string> WidgetReserved =
        new(StringComparer.OrdinalIgnoreCase) { "v1" };

    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ITenantContext tenant, EquineDbContext db)
    {
        var path = context.Request.Path;

        if (TryGetSlug(path, "/api/public/", PublicReserved, out var slug)
            || TryGetSlug(path, "/widget/", WidgetReserved, out slug)
            || TryGetSlug(path, "/portal/", null, out slug))
        {
            var row = await db.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Slug == slug, context.RequestAborted);
            if (row is null || row.Status == TenantStatus.Suspended)
            {
                if (path.StartsWithSegments("/api"))
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    await context.Response.WriteAsJsonAsync(new { message = "Okänd verksamhet." });
                    return;
                }
            }
            else
            {
                tenant.SetTenant(row.Id, row.Slug);
            }
        }
        else if (context.User.Identity?.IsAuthenticated == true)
        {
            var claim = context.User.FindFirst("tenantId")?.Value;
            if (Guid.TryParse(claim, out var id) && id != Guid.Empty)
            {
                var row = await db.Tenants.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == id, context.RequestAborted);
                if (row is null || row.Status == TenantStatus.Suspended)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new { message = "Verksamheten är inte aktiv." });
                    return;
                }

                tenant.SetTenant(row.Id, row.Slug);
            }
        }

        await _next(context);
    }

    private static bool TryGetSlug(PathString path, string prefix, HashSet<string>? reserved, out string slug)
    {
        slug = "";
        var value = path.Value ?? "";
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var rest = value[prefix.Length..].Trim('/');
        if (rest.Length == 0) return false;
        var segment = rest.Split('/', 2)[0];
        if (reserved is not null && reserved.Contains(segment))
            return false;
        slug = segment;
        return true;
    }
}
