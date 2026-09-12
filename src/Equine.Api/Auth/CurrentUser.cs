using System.Security.Claims;

namespace Equine.Api.Auth;

public static class CurrentUser
{
    public static Guid? GetUserId(HttpContext context)
    {
        var raw = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub")
            ?? context.User.FindFirstValue(JwtRegisteredClaimNamesFallback);
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    public static string GetDisplayName(HttpContext context)
    {
        return context.User.FindFirstValue("displayName")
            ?? context.User.FindFirstValue(ClaimTypes.Email)
            ?? GetUserId(context)?.ToString()
            ?? "unknown";
    }

    public static string? GetActorId(HttpContext context) => GetUserId(context)?.ToString();

    public static Guid? GetTenantId(HttpContext context)
    {
        var raw = context.User.FindFirstValue("tenantId");
        return Guid.TryParse(raw, out var id) && id != Guid.Empty ? id : null;
    }

    private const string JwtRegisteredClaimNamesFallback = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier";
}
