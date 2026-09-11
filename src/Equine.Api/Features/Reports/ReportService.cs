using Equine.Domain.Entities;
using Equine.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Equine.Api.Features.Reports;

public sealed class ReportService
{
    private static readonly TimeZoneInfo Stockholm = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");

    private readonly EquineDbContext _db;

    public ReportService(EquineDbContext db) => _db = db;

    public async Task<IReadOnlyList<TreatmentPeriodRow>> TreatmentsPerPeriodAsync(
        DateOnly from, DateOnly to, CancellationToken ct)
    {
        var start = ToUtcStart(from);
        var end = ToUtcEnd(to);

        return await _db.JournalEntries
            .Where(j => j.Status == JournalStatus.Signed
                        && j.PerformedAt >= start && j.PerformedAt < end
                        && j.TreatmentTypeId != null)
            .GroupBy(j => new
            {
                Month = new DateTime(j.PerformedAt.Year, j.PerformedAt.Month, 1),
                j.TreatmentTypeId,
                j.TreatmentTypeName
            })
            .Select(g => new TreatmentPeriodRow(
                DateOnly.FromDateTime(g.Key.Month),
                g.Key.TreatmentTypeName ?? "Okänd",
                g.Count(),
                g.Sum(j => j.Price ?? 0)))
            .OrderBy(r => r.Month)
            .ThenBy(r => r.TreatmentName)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<RevenueRow>> RevenueOverviewAsync(
        DateOnly from, DateOnly to, CancellationToken ct)
    {
        var start = ToUtcStart(from);
        var end = ToUtcEnd(to);

        var lines = await _db.BookingLines
            .Include(l => l.Visit)
            .Where(l => l.Status == BookingStatus.Completed
                        && l.Visit.StartsAt >= start && l.Visit.StartsAt < end)
            .ToListAsync(ct);

        return lines
            .GroupBy(l => DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(l.Visit.StartsAt, Stockholm).DateTime))
            .Select(g =>
            {
                var excl = g.Sum(l => l.Price);
                var incl = g.Sum(l => l.Price * (1 + l.VatRate));
                return new RevenueRow(
                    new DateTime(g.Key.Year, g.Key.Month, 1),
                    excl,
                    incl,
                    g.Count());
            })
            .OrderBy(r => r.Month)
            .ToList();
    }

    public async Task<IReadOnlyList<FollowUpReportRow>> FollowUpsAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var horses = await _db.Horses
            .Include(h => h.Owner)
            .Include(h => h.JournalEntries)
            .Where(h => h.FollowUpDismissedAt == null)
            .ToListAsync(ct);

        var treatments = await _db.TreatmentTypes.ToDictionaryAsync(t => t.Id, ct);
        var rows = new List<FollowUpReportRow>();

        foreach (var horse in horses)
        {
            if (horse.FollowUpSnoozedUntil is DateTimeOffset snooze && snooze > now)
                continue;

            var last = horse.JournalEntries
                .Where(j => j.Status == JournalStatus.Signed)
                .OrderByDescending(j => j.PerformedAt)
                .FirstOrDefault();
            if (last is null) continue;

            var interval = horse.FollowUpOverrideDays
                           ?? (last.TreatmentTypeId is Guid tid && treatments.TryGetValue(tid, out var t)
                               ? t.FollowUpIntervalDays
                               : null);
            if (interval is null) continue;

            var due = last.PerformedAt.AddDays(interval.Value);
            if (due > now) continue;

            rows.Add(new FollowUpReportRow(
                horse.Name,
                horse.Owner.Name,
                last.TreatmentTypeName ?? "",
                last.PerformedAt,
                due,
                (int)(now - due).TotalDays));
        }

        return rows.OrderByDescending(r => r.DaysOverdue).ToList();
    }

    public async Task<IReadOnlyList<UnsignedJournalRow>> UnsignedJournalsAsync(CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow.AddHours(-24);
        return await _db.JournalEntries
            .Where(j => j.Status == JournalStatus.Draft && j.CreatedAt <= cutoff)
            .OrderBy(j => j.CreatedAt)
            .Select(j => new UnsignedJournalRow(
                j.Id,
                j.Horse.Name,
                j.Horse.Owner.Name,
                j.TreatmentTypeName ?? "",
                j.CreatedAt,
                (int)(DateTimeOffset.UtcNow - j.CreatedAt).TotalHours))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<NoShowRow>> NoShowsAsync(
        DateOnly from, DateOnly to, CancellationToken ct)
    {
        var start = ToUtcStart(from);
        var end = ToUtcEnd(to);

        var lines = await _db.BookingLines
            .Include(l => l.Visit)
            .Include(l => l.Owner)
            .Include(l => l.Horse)
            .Where(l => l.Visit.StartsAt >= start && l.Visit.StartsAt < end
                        && (l.Status == BookingStatus.NoShow
                            || (l.Status == BookingStatus.Cancelled && l.CancelledAt != null
                                && l.CancelledAt > l.Visit.StartsAt.AddHours(-24))))
            .ToListAsync(ct);

        return lines
            .GroupBy(l => l.OwnerId)
            .Select(g =>
            {
                var first = g.First();
                return new NoShowRow(
                    first.Owner.Name,
                    first.Owner.Email ?? "",
                    g.Count(l => l.Status == BookingStatus.NoShow),
                    g.Count(l => l.Status == BookingStatus.Cancelled),
                    g.Count());
            })
            .OrderByDescending(r => r.Total)
            .ToList();
    }

    public async Task<IReadOnlyList<DormantClientRow>> DormantClientsAsync(
        int months, CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow.AddMonths(-months);
        var activeOwnerIds = await _db.BookingLines
            .Include(l => l.Visit)
            .Where(l => l.Visit.StartsAt >= cutoff
                        && l.Status != BookingStatus.Cancelled)
            .Select(l => l.OwnerId)
            .Distinct()
            .ToListAsync(ct);

        var owners = await _db.Owners
            .Where(o => !activeOwnerIds.Contains(o.Id) && o.DeletedAt == null)
            .OrderBy(o => o.Name)
            .ToListAsync(ct);

        var lastBookings = await _db.BookingLines
            .Include(l => l.Visit)
            .Where(l => owners.Select(o => o.Id).Contains(l.OwnerId))
            .GroupBy(l => l.OwnerId)
            .Select(g => new { OwnerId = g.Key, LastAt = g.Max(l => l.Visit.StartsAt) })
            .ToDictionaryAsync(x => x.OwnerId, x => x.LastAt, ct);

        return owners.Select(o => new DormantClientRow(
            o.Name,
            o.Email ?? "",
            o.Phone ?? "",
            lastBookings.TryGetValue(o.Id, out var last) ? last : null)).ToList();
    }

    public async Task<IReadOnlyList<BookingSourceRow>> BookingSourcesAsync(
        DateOnly from, DateOnly to, CancellationToken ct)
    {
        var start = ToUtcStart(from);
        var end = ToUtcEnd(to);

        var lines = await _db.BookingLines
            .Include(l => l.Visit)
            .Where(l => l.Visit.StartsAt >= start && l.Visit.StartsAt < end
                        && l.Status != BookingStatus.Cancelled)
            .ToListAsync(ct);

        return lines
            .GroupBy(l => new
            {
                Month = DateOnly.FromDateTime(
                    TimeZoneInfo.ConvertTime(l.Visit.StartsAt, Stockholm).DateTime),
                l.Source
            })
            .Select(g => new BookingSourceRow(
                new DateTime(g.Key.Month.Year, g.Key.Month.Month, 1),
                g.Key.Source.ToString(),
                g.Count()))
            .OrderBy(r => r.Month)
            .ThenBy(r => r.Source)
            .ToList();
    }

    private static DateTimeOffset ToUtcStart(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, Stockholm.GetUtcOffset(local)).ToUniversalTime();
    }

    private static DateTimeOffset ToUtcEnd(DateOnly date) => ToUtcStart(date.AddDays(1));
}

public sealed record TreatmentPeriodRow(DateOnly Month, string TreatmentName, int Count, decimal RevenueEstimate);
public sealed record RevenueRow(DateTime Month, decimal ExclVat, decimal InclVat, int BookingCount);
public sealed record FollowUpReportRow(string HorseName, string OwnerName, string LastTreatment, DateTimeOffset LastPerformedAt, DateTimeOffset DueAt, int DaysOverdue);
public sealed record UnsignedJournalRow(Guid Id, string HorseName, string OwnerName, string Treatment, DateTimeOffset CreatedAt, int HoursOld);
public sealed record NoShowRow(string OwnerName, string Email, int NoShows, int LateCancellations, int Total);
public sealed record DormantClientRow(string Name, string Email, string Phone, DateTimeOffset? LastBookingAt);
public sealed record BookingSourceRow(DateTime Month, string Source, int Count);
