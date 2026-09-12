namespace Equine.Domain.Common;

public static class TenantSlug
{
    private static readonly HashSet<string> Reserved =
    [
        "api", "app", "admin", "widget", "portal", "health", "swagger", "register",
        "login", "www", "static", "assets", "public"
    ];

    public static string FromName(string? name)
    {
        var slug = TreatmentSlug.FromName(name);
        if (Reserved.Contains(slug))
            slug = $"{slug}-klinik";
        return slug;
    }

    public static bool IsReserved(string slug) => Reserved.Contains(slug);
}
