using System.Security.Claims;
using Equine.Api.Auth;
using Equine.Domain.Entities;
using Equine.Infrastructure;
using Equine.Infrastructure.Notifications;
using Equine.Infrastructure.Practice;
using Equine.Infrastructure.Tokens;
using ConsumedMagicLinkStore = Equine.Infrastructure.Tokens.ConsumedMagicLinkStore;
using Equine.Infrastructure.Widget;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Equine.Api.Features.Portal;

public static class PortalEndpoints
{
    public static IEndpointRouteBuilder MapPortalEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/public/auth/magic-link", async (
            MagicLinkRequest request,
            EquineDbContext db,
            INotificationScheduler scheduler,
            PracticeSettingsService practice,
            WidgetSettingsService widget,
            PublicBookingTokenService tokens,
            CancellationToken ct) =>
        {
            var email = (request.Email ?? "").Trim();
            if (string.IsNullOrWhiteSpace(email))
                return Results.Ok();

            var owner = await db.Owners.FirstOrDefaultAsync(o => o.Email == email, ct);
            if (owner is not null && !owner.EmailInvalid)
            {
                var token = tokens.Protect(owner.Id, DateTimeOffset.UtcNow.AddMinutes(15), PublicBookingTokenService.MagicLinkPurpose);
                var w = await widget.GetAsync(ct);
                var p = await practice.GetAsync(ct);
                var url = $"{w.PublicBaseUrl}/portal/in?token={Uri.EscapeDataString(token)}";
                await scheduler.EnqueueAsync(
                    NotificationType.MagicLink,
                    NotificationChannel.Email,
                    owner.Email,
                    nameof(Owner),
                    owner.Id,
                    DateTimeOffset.UtcNow,
                    new Dictionary<string, string>
                    {
                        ["klientnamn"] = owner.Name,
                        ["kliniknamn"] = p.Clinic,
                        ["magiclänk"] = url,
                        ["magiclank"] = url
                    },
                    ct);
            }

            return Results.Ok();
        }).RequireRateLimiting("public-book").WithName("RequestMagicLink");

        app.MapPost("/api/public/auth/magic-link/exchange", async (
            MagicLinkExchangeRequest request,
            EquineDbContext db,
            PublicBookingTokenService tokens,
            ConsumedMagicLinkStore consumed,
            UserManager<ApplicationUser> users,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Token))
                return Results.BadRequest(new { error = "Ogiltig länk." });

            var tokenHash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(request.Token)));

            if (await consumed.IsConsumedAsync(tokenHash, ct))
                return Results.BadRequest(new { error = "Länken har redan använts." });

            if (!tokens.TryUnprotect(request.Token, PublicBookingTokenService.MagicLinkPurpose, out var ownerId, out var expired)
                || expired)
                return Results.BadRequest(new { error = "Ogiltig länk." });

            await consumed.ConsumeAsync(tokenHash, ct);

            var owner = await db.Owners.FirstOrDefaultAsync(o => o.Id == ownerId, ct);
            if (owner is null) return Results.BadRequest(new { error = "Ogiltig länk." });

            ApplicationUser? user = null;
            if (owner.UserId is Guid uid)
                user = await users.FindByIdAsync(uid.ToString());
            if (user is null)
            {
                user = new ApplicationUser
                {
                    Id = Guid.CreateVersion7(),
                    Email = owner.Email,
                    UserName = owner.Email,
                    DisplayName = owner.Name,
                    EmailConfirmed = true,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                var created = await users.CreateAsync(user, $"Cli!{Guid.NewGuid():N}aA1");
                if (!created.Succeeded)
                    return Results.BadRequest(new { error = "Kunde inte skapa session." });
                await users.AddToRoleAsync(user, "Client");
                owner.LinkUser(user.Id);
                await db.SaveChangesAsync(ct);
            }

            var jwt = configuration.GetSection("Jwt");
            var key = System.Text.Encoding.ASCII.GetBytes(jwt["SecurityKey"]!);
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Email, user.Email ?? ""),
                new("displayName", user.DisplayName ?? owner.Name),
                new("ownerId", owner.Id.ToString()),
                new(ClaimTypes.Role, "Client")
            };
            var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
                jwt["Issuer"],
                jwt["Audience"],
                claims,
                expires: DateTime.UtcNow.AddHours(2),
                signingCredentials: new Microsoft.IdentityModel.Tokens.SigningCredentials(
                    new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
                    Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256));
            var access = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
            return Results.Ok(new { accessToken = access, ownerId = owner.Id });
        }).RequireRateLimiting("public-read").WithName("ExchangeMagicLink");

        app.MapGet("/api/public/unsubscribe", async (
            string token,
            EquineDbContext db,
            PublicBookingTokenService tokens,
            CancellationToken ct) =>
        {
            if (!tokens.TryUnprotect(token, PublicBookingTokenService.UnsubscribePurpose, out var ownerId, out var expired)
                || expired)
                return Results.BadRequest();
            var owner = await db.Owners.FirstOrDefaultAsync(o => o.Id == ownerId, ct);
            if (owner is not null)
            {
                owner.SetMarketingConsent(false);
                await db.SaveChangesAsync(ct);
            }
            return Results.Ok(new { unsubscribed = true });
        }).WithName("MarketingUnsubscribe");

        var portal = app.MapGroup("/api/app/portal").RequireAuthorization();

        portal.MapGet("/me", async (HttpContext http, EquineDbContext db, CancellationToken ct) =>
        {
            var owner = await RequireOwner(http, db, ct);
            if (owner is null) return Results.Forbid();
            return Results.Ok(new
            {
                owner.Id,
                owner.Name,
                owner.Email,
                owner.Phone,
                owner.AddressStreet,
                owner.AddressPostcode,
                owner.AddressCity,
                owner.MarketingConsent
            });
        }).WithName("PortalMe");

        portal.MapPut("/me", async (HttpContext http, PortalProfileRequest request, EquineDbContext db, CancellationToken ct) =>
        {
            var owner = await RequireOwner(http, db, ct);
            if (owner is null) return Results.Forbid();
            owner.Update(
                request.Name,
                request.Email,
                request.Phone,
                request.AddressStreet,
                request.AddressPostcode,
                request.AddressCity,
                marketingConsent: request.MarketingConsent);
            await db.SaveChangesAsync(ct);
            return Results.Ok();
        }).WithName("PortalUpdateMe");

        portal.MapGet("/horses", async (HttpContext http, EquineDbContext db, CancellationToken ct) =>
        {
            var owner = await RequireOwner(http, db, ct);
            if (owner is null) return Results.Forbid();
            var horses = await db.Horses.Where(h => h.OwnerId == owner.Id)
                .Select(h => new { h.Id, h.Name, h.Breed, h.BirthYear, h.AgeGroup })
                .ToListAsync(ct);
            return Results.Ok(horses);
        }).WithName("PortalHorses");

        portal.MapGet("/bookings", async (HttpContext http, EquineDbContext db, CancellationToken ct) =>
        {
            var owner = await RequireOwner(http, db, ct);
            if (owner is null) return Results.Forbid();
            var lines = await db.BookingLines
                .Include(l => l.Visit)
                .Include(l => l.Horse)
                .Include(l => l.TreatmentType)
                .Where(l => l.OwnerId == owner.Id)
                .ToListAsync(ct);
            return Results.Ok(lines.Select(l => new
            {
                l.Id,
                l.TreatmentName,
                treatmentSlug = l.TreatmentType?.Slug,
                horseName = l.Horse?.Name,
                horseId = l.HorseId,
                startsAt = l.Visit.DeriveLineStart(l),
                l.Status,
                shareSummary = l.TreatmentType?.ShareSummaryWithClient == true
            }));
        }).WithName("PortalBookings");

        portal.MapGet("/horses/{horseId:guid}/summary", async (
            Guid horseId,
            HttpContext http,
            EquineDbContext db,
            CancellationToken ct) =>
        {
            var owner = await RequireOwner(http, db, ct);
            if (owner is null) return Results.Forbid();
            var horse = await db.Horses.FirstOrDefaultAsync(h => h.Id == horseId && h.OwnerId == owner.Id, ct);
            if (horse is null) return Results.NotFound();
            var entries = await db.JournalEntries
                .Where(j => j.HorseId == horseId && j.Status == JournalStatus.Signed)
                .OrderByDescending(j => j.PerformedAt)
                .ToListAsync(ct);
            var treatments = await db.TreatmentTypes.ToDictionaryAsync(t => t.Id, ct);
            var summaries = entries
                .Where(j => j.TreatmentTypeId is Guid tid && treatments.TryGetValue(tid, out var t) && t.ShareSummaryWithClient)
                .Select(j => new
                {
                    j.Id,
                    j.PerformedAt,
                    j.TreatmentTypeName,
                    plan = j.PrognosOchPlan
                });
            return Results.Ok(summaries);
        }).WithName("PortalHorseSummary");

        return app;
    }

    private static async Task<Owner?> RequireOwner(HttpContext http, EquineDbContext db, CancellationToken ct)
    {
        if (!http.User.IsInRole("Client"))
            return null;
        var ownerClaim = http.User.FindFirstValue("ownerId");
        if (Guid.TryParse(ownerClaim, out var ownerId))
            return await db.Owners.FirstOrDefaultAsync(o => o.Id == ownerId, ct);
        var userId = CurrentUser.GetUserId(http);
        if (userId is null) return null;
        return await db.Owners.FirstOrDefaultAsync(o => o.UserId == userId, ct);
    }
}

public sealed record MagicLinkRequest(string? Email);
public sealed record MagicLinkExchangeRequest(string Token);
public sealed record PortalProfileRequest(
    string? Name,
    string? Email,
    string? Phone,
    string? AddressStreet,
    string? AddressPostcode,
    string? AddressCity,
    bool? MarketingConsent);
