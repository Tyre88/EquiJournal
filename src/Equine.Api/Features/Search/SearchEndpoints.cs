using Equine.Domain.Entities;
using Equine.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Equine.Api.Features.Search;

public static class SearchEndpoints
{
    private static readonly TimeZoneInfo Stockholm = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");

    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/app/search", async (
            [FromQuery] string? q,
            [FromQuery] int? limit,
            EquineDbContext db,
            CancellationToken ct) =>
        {
            var query = (q ?? "").Trim();
            if (query.Length < 2)
                return Results.Ok(new { clients = Array.Empty<object>(), horses = Array.Empty<object>(), journals = Array.Empty<object>(), bookings = Array.Empty<object>() });

            var take = Math.Clamp(limit is null or <= 0 ? 10 : limit.Value, 1, 30);
            var pattern = $"%{query}%";

            var clients = await db.Owners
                .Where(o => o.DeletedAt == null
                            && (EF.Functions.ILike(o.Name, pattern)
                                || EF.Functions.ILike(o.Email ?? "", pattern)
                                || EF.Functions.ILike(o.Phone ?? "", pattern)))
                .OrderBy(o => o.Name)
                .Take(take)
                .Select(o => new { o.Id, o.Name, o.Email, o.Phone, type = "client" })
                .ToListAsync(ct);

            var horses = await db.Horses
                .Where(h => h.DeletedAt == null
                            && (EF.Functions.ILike(h.Name, pattern)
                                || EF.Functions.ILike(h.Breed ?? "", pattern)
                                || EF.Functions.ILike(h.Colour ?? "", pattern)
                                || EF.Functions.ILike(h.Owner.Name, pattern)))
                .OrderBy(h => h.Name)
                .Take(take)
                .Select(h => new { h.Id, h.Name, ownerName = h.Owner.Name, type = "horse" })
                .ToListAsync(ct);

            List<JournalHit> journals;
            try
            {
                var journalIds = await db.Database.SqlQuery<Guid>($"""
                    SELECT "Id" AS "Value"
                    FROM journal_entries
                    WHERE "search_vector" @@ plainto_tsquery('swedish', {query})
                    ORDER BY "PerformedAt" DESC
                    LIMIT {take}
                    """).ToListAsync(ct);

                journals = await db.JournalEntries
                    .Where(j => journalIds.Contains(j.Id))
                    .OrderByDescending(j => j.PerformedAt)
                    .Select(j => new JournalHit
                    {
                        Id = j.Id,
                        HorseName = j.Horse.Name,
                        OwnerName = j.Horse.Owner.Name,
                        PerformedAt = j.PerformedAt
                    })
                    .ToListAsync(ct);
            }
            catch
            {
                journals = [];
            }

            var bookings = DateOnly.TryParse(query, out var date)
                ? await db.BookingLines
                    .Where(l => l.Visit.StartsAt >= ToUtc(date) && l.Visit.StartsAt < ToUtc(date.AddDays(1)))
                    .OrderBy(l => l.Visit.StartsAt)
                    .Take(take)
                    .Select(l => new BookingHit
                    {
                        Id = l.Id,
                        HorseName = l.Horse.Name,
                        OwnerName = l.Owner.Name,
                        TreatmentName = l.TreatmentName,
                        StartsAt = l.Visit.StartsAt
                    })
                    .ToListAsync(ct)
                : await db.BookingLines
                    .Where(l => EF.Functions.ILike(l.Horse.Name, pattern)
                                || EF.Functions.ILike(l.Owner.Name, pattern)
                                || EF.Functions.ILike(l.TreatmentName, pattern))
                    .OrderByDescending(l => l.Visit.StartsAt)
                    .Take(take)
                    .Select(l => new BookingHit
                    {
                        Id = l.Id,
                        HorseName = l.Horse.Name,
                        OwnerName = l.Owner.Name,
                        TreatmentName = l.TreatmentName,
                        StartsAt = l.Visit.StartsAt
                    })
                    .ToListAsync(ct);

            return Results.Ok(new
            {
                clients,
                horses,
                journals = journals.Select(j => new
                {
                    j.Id,
                    j.HorseName,
                    j.OwnerName,
                    j.PerformedAt,
                    type = "journal"
                }),
                bookings = bookings.Select(b => new
                {
                    b.Id,
                    horseName = b.HorseName,
                    ownerName = b.OwnerName,
                    treatmentName = b.TreatmentName,
                    startsAt = b.StartsAt,
                    type = "booking"
                })
            });
        })
        .RequireAuthorization("CanManageBookings")
        .WithName("GlobalSearch");

        return app;
    }

    private sealed class JournalHit
    {
        public Guid Id { get; set; }
        public string HorseName { get; set; } = "";
        public string OwnerName { get; set; } = "";
        public DateTimeOffset PerformedAt { get; set; }
    }

    private sealed class BookingHit
    {
        public Guid Id { get; set; }
        public string HorseName { get; set; } = "";
        public string OwnerName { get; set; } = "";
        public string TreatmentName { get; set; } = "";
        public DateTimeOffset StartsAt { get; set; }
    }

    private static DateTimeOffset ToUtc(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, Stockholm.GetUtcOffset(local)).ToUniversalTime();
    }
}
