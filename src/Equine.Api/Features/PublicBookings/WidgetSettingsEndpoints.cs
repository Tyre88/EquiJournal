using Equine.Infrastructure;
using Equine.Infrastructure.Widget;
using Microsoft.EntityFrameworkCore;

namespace Equine.Api.Features.PublicBookings;

public static class WidgetSettingsEndpoints
{
    public static IEndpointRouteBuilder MapWidgetSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/app/widget-settings").RequireAuthorization("CanAdminister");

        group.MapGet("/", async (WidgetSettingsService settings, EquineDbContext db, CancellationToken ct) =>
        {
            var s = await settings.GetAsync(ct);
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
                s.PublicBaseUrl,
                s.ContactPhone,
                s.ContactEmail,
                treatments,
                embedSnippet = EmbedSnippet(s.PublicBaseUrl),
                treatmentSnippets = treatments.Where(t => !string.IsNullOrWhiteSpace(t.Slug))
                    .Select(t => new { t.Id, t.Name, t.Slug, snippet = EmbedSnippet(s.PublicBaseUrl, t.Slug) })
            });
        }).WithName("GetWidgetSettings");

        group.MapPut("/", async (WidgetSettingsUpdateRequest request, WidgetSettingsService settings, CancellationToken ct) =>
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
            return Results.Ok(new
            {
                allowedOrigins = s.GetAllowedOrigins(),
                s.ShowPrices,
                s.BookingTerms,
                s.PrivacyPolicyUrl,
                s.VerificationWindowMinutes,
                s.CancellationNoticeHours,
                s.PublicBaseUrl,
                s.ContactPhone,
                s.ContactEmail,
                embedSnippet = EmbedSnippet(s.PublicBaseUrl)
            });
        }).WithName("UpdateWidgetSettings");

        return app;
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
