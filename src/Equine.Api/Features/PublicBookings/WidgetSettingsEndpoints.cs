using Equine.Infrastructure;
using Equine.Infrastructure.Widget;
using Microsoft.EntityFrameworkCore;

namespace Equine.Api.Features.PublicBookings;

public static class WidgetSettingsEndpoints
{
    public static IEndpointRouteBuilder MapWidgetSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/app/widget-settings").RequireAuthorization("CanAdminister");

        group.MapGet("/", async (HttpContext http, WidgetSettingsService settings, EquineDbContext db, CancellationToken ct) =>
        {
            var s = await settings.GetAsync(ct);
            var publicBaseUrl = ResolvePublicBaseUrl(s.PublicBaseUrl, http.Request);
            var treatments = await db.TreatmentTypes
                .OrderBy(t => t.Name)
                .Select(t => new { t.Id, t.Name, t.Slug, t.BookableOnline, t.RequiresApproval, t.Status })
                .ToListAsync(ct);
            return Results.Ok(new
            {
                allowedOrigins = s.GetAllowedOrigins(),
                s.ShowPrices,
                s.BookingTerms,
                s.PrivacyPolicyUrl,
                s.VerificationWindowMinutes,
                s.CancellationNoticeHours,
                publicBaseUrl,
                s.ContactPhone,
                s.ContactEmail,
                treatments,
                embedSnippet = EmbedSnippet(publicBaseUrl),
                treatmentSnippets = treatments.Where(t => !string.IsNullOrWhiteSpace(t.Slug))
                    .Select(t => new { t.Id, t.Name, t.Slug, snippet = EmbedSnippet(publicBaseUrl, t.Slug) })
            });
        }).WithName("GetWidgetSettings");

        group.MapPut("/", async (HttpContext http, WidgetSettingsUpdateRequest request, WidgetSettingsService settings, CancellationToken ct) =>
        {
            var origins = (request.AllowedOrigins ?? [])
                .Select(o => o.Trim().TrimEnd('/'))
                .Where(o => o.Length > 0 && !o.Contains('*', StringComparison.Ordinal))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var s = await settings.UpdateAsync(
                origins,
                request.ShowPrices,
                request.BookingTerms,
                request.PrivacyPolicyUrl,
                request.VerificationWindowMinutes,
                request.CancellationNoticeHours,
                request.PublicBaseUrl,
                request.ContactPhone,
                request.ContactEmail,
                ct);
            var publicBaseUrl = ResolvePublicBaseUrl(s.PublicBaseUrl, http.Request);
            return Results.Ok(new
            {
                allowedOrigins = s.GetAllowedOrigins(),
                s.ShowPrices,
                s.BookingTerms,
                s.PrivacyPolicyUrl,
                s.VerificationWindowMinutes,
                s.CancellationNoticeHours,
                publicBaseUrl,
                s.ContactPhone,
                s.ContactEmail,
                embedSnippet = EmbedSnippet(publicBaseUrl)
            });
        }).WithName("UpdateWidgetSettings");

        return app;
    }

    internal static string ResolvePublicBaseUrl(string? stored, HttpRequest request)
    {
        if (!IsUnsetOrLoopback(stored))
            return stored!.TrimEnd('/');
        return $"{request.Scheme}://{request.Host.Value}".TrimEnd('/');
    }

    internal static bool IsUnsetOrLoopback(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return true;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return true;
        return uri.IsLoopback;
    }

    public static string EmbedSnippet(string publicBaseUrl, string? treatmentSlug = null)
    {
        var treatmentAttr = string.IsNullOrWhiteSpace(treatmentSlug)
            ? ""
            : $"\n        data-treatment=\"{treatmentSlug}\"";
        return $"""
        <div id="hastbokning"></div>
        <script src="{publicBaseUrl.TrimEnd('/')}/widget/v1/loader.js"
                data-target="#hastbokning"
                data-lang="sv"{treatmentAttr}
                async></script>
        """;
    }

    public record WidgetSettingsUpdateRequest(
        IReadOnlyList<string>? AllowedOrigins = null,
        bool? ShowPrices = null,
        string? BookingTerms = null,
        string? PrivacyPolicyUrl = null,
        int? VerificationWindowMinutes = null,
        int? CancellationNoticeHours = null,
        string? PublicBaseUrl = null,
        string? ContactPhone = null,
        string? ContactEmail = null);
}
