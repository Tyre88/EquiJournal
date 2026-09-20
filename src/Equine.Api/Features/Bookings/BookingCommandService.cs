using Equine.Api.Features.Availability;
using Equine.Domain.Entities;
using Equine.Domain.Scheduling;
using Equine.Infrastructure;
using Equine.Infrastructure.Audit;
using Equine.Infrastructure.Notifications;
using Equine.Infrastructure.Scheduling;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Equine.Api.Features.Bookings;

public sealed class BookingCommandService
{
    private readonly EquineDbContext _db;
    private readonly LocationResolver _locations;
    private readonly SlotQueryService _slots;
    private readonly ISlotCache _cache;
    private readonly IAuditWriter _audit;
    private readonly BookingNotifier _notifier;

    public BookingCommandService(
        EquineDbContext db,
        LocationResolver locations,
        SlotQueryService slots,
        ISlotCache cache,
        IAuditWriter audit,
        BookingNotifier notifier)
    {
        _db = db;
        _locations = locations;
        _slots = slots;
        _cache = cache;
        _audit = audit;
        _notifier = notifier;
    }

    public async Task<Visit> CreateManualAsync(
        Guid practitionerId,
        Guid actorId,
        DateTimeOffset startsAt,
        DateTimeOffset? endsAtOverride,
        Guid? locationId,
        BookingSource source,
        IReadOnlyList<BookingLineRequest> lines,
        CancellationToken cancellationToken = default)
    {
        if (lines.Count == 0)
            throw new ArgumentException("At least one booking line is required.");

        var horseIds = lines.Select(l => l.HorseId).Distinct().ToList();
        var horses = await _db.Horses.Include(h => h.Owner)
            .Where(h => horseIds.Contains(h.Id))
            .ToListAsync(cancellationToken);
        if (horses.Count != horseIds.Count)
            throw new ArgumentException("Horse not found.");

        var treatmentIds = lines.Select(l => l.TreatmentTypeId).Distinct().ToList();
        var treatments = await _db.TreatmentTypes
            .Where(t => treatmentIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, cancellationToken);
        if (treatments.Count != treatmentIds.Count)
            throw new ArgumentException("Treatment type not found.");

        Location location;
        if (locationId is Guid explicitId)
        {
            location = await _db.Locations.FirstOrDefaultAsync(l => l.Id == explicitId, cancellationToken)
                ?? throw new ArgumentException("Location not found.");
        }
        else
        {
            var firstHorse = horses.First(h => h.Id == lines[0].HorseId);
            location = await _locations.ResolveForHorseAsync(firstHorse, firstHorse.Owner, cancellationToken);
        }

        foreach (var horse in horses)
        {
            if (!LocationResolver.SameStable(LocationResolver.KeyFor(location), LocationResolver.KeyFor(horse, horse.Owner)))
                throw new ArgumentException("Alla hästar på ett besök måste vara på samma stall.");
        }

        var visit = new Visit(practitionerId, location.Id, startsAt.ToUniversalTime(), source);
        _db.Visits.Add(visit);

        var sort = 0;
        foreach (var req in lines)
        {
            var horse = horses.First(h => h.Id == req.HorseId);
            var treatment = treatments[req.TreatmentTypeId];
            var line = new BookingLine(
                visit.Id,
                horse.Id,
                horse.OwnerId,
                treatment.Id,
                treatment.Name,
                treatment.DurationMinutes,
                treatment.PriceExclVat,
                treatment.VatRate,
                BookingStatus.Confirmed,
                source,
                sort++,
                req.ClientNote,
                req.InternalNote);
            visit.AddLine(line);
        }

        visit.RecalculateTiming();
        if (endsAtOverride is DateTimeOffset forced && forced > visit.StartsAt)
            visit.SetEndsAt(forced);

        await SaveVisitCatchingOverlap(visit, lines[0].TreatmentTypeId, location.AddressPostcode, cancellationToken);

        await _audit.WriteAsync(
            actorId.ToString(),
            "BOOKING_CREATE",
            nameof(Visit),
            visit.Id.ToString(),
            null,
            $"{visit.StartsAt:o}|{visit.Lines.Count}");
        await _db.SaveChangesAsync(cancellationToken);
        _cache.InvalidateAll();
        return visit;
    }

    public async Task<BookingLine> AddLineAsync(
        Guid visitId,
        Guid actorId,
        BookingLineRequest request,
        CancellationToken cancellationToken = default)
    {
        var visit = await LoadVisitAsync(visitId, cancellationToken);
        var horse = await _db.Horses.Include(h => h.Owner).FirstOrDefaultAsync(h => h.Id == request.HorseId, cancellationToken)
            ?? throw new ArgumentException("Horse not found.");
        var treatment = await _db.TreatmentTypes.FirstOrDefaultAsync(t => t.Id == request.TreatmentTypeId, cancellationToken)
            ?? throw new ArgumentException("Treatment type not found.");

        await _db.Entry(visit).Reference(v => v.Location).LoadAsync(cancellationToken);
        if (!LocationResolver.SameStable(LocationResolver.KeyFor(visit.Location), LocationResolver.KeyFor(horse, horse.Owner)))
            throw new ArgumentException("Hästen står på ett annat stall. Skapa ett nytt besök.");

        var line = new BookingLine(
            visit.Id,
            horse.Id,
            horse.OwnerId,
            treatment.Id,
            treatment.Name,
            treatment.DurationMinutes,
            treatment.PriceExclVat,
            treatment.VatRate,
            BookingStatus.Confirmed,
            visit.Source,
            visit.NextSortOrder(),
            request.ClientNote,
            request.InternalNote);
        visit.AddLine(line);

        await SaveVisitCatchingOverlap(visit, treatment.Id, visit.Location.AddressPostcode, cancellationToken);
        await _audit.WriteAsync(actorId.ToString(), "BOOKING_LINE_ADD", nameof(BookingLine), line.Id.ToString(), null, null);
        await _db.SaveChangesAsync(cancellationToken);
        _cache.InvalidateAll();
        return line;
    }

    public async Task RescheduleAsync(
        Guid visitId,
        Guid actorId,
        DateTimeOffset startsAt,
        CancellationToken cancellationToken = default)
    {
        var visit = await LoadVisitAsync(visitId, cancellationToken);
        await _db.Entry(visit).Reference(v => v.Location).LoadAsync(cancellationToken);
        var before = visit.StartsAt.ToString("o");
        visit.Reschedule(startsAt.ToUniversalTime());
        var treatmentId = visit.Lines.OrderBy(l => l.SortOrder).Select(l => l.TreatmentTypeId).FirstOrDefault();
        await SaveVisitCatchingOverlap(visit, treatmentId, visit.Location.AddressPostcode, cancellationToken);
        await _audit.WriteAsync(actorId.ToString(), "BOOKING_RESCHEDULE", nameof(Visit), visit.Id.ToString(), before, visit.StartsAt.ToString("o"));
        await _db.SaveChangesAsync(cancellationToken);
        _cache.InvalidateAll();
        foreach (var line in visit.Lines.Where(l => l.Status is BookingStatus.Confirmed or BookingStatus.Requested))
        {
            await _db.Entry(line).Reference(l => l.Owner).LoadAsync(cancellationToken);
            await _db.Entry(line).Reference(l => l.Horse).LoadAsync(cancellationToken);
            if (line.Owner is not null)
                await _notifier.OnRescheduledAsync(line, line.Owner.Email, line.Owner.Name, cancellationToken);
        }
    }

    public async Task ChangeLocationAsync(Guid visitId, Guid locationId, Guid actorId, CancellationToken cancellationToken = default)
    {
        var visit = await LoadVisitAsync(visitId, cancellationToken);
        var location = await _db.Locations.FirstOrDefaultAsync(l => l.Id == locationId, cancellationToken)
            ?? throw new ArgumentException("Location not found.");
        visit.ChangeLocation(location.Id);
        await _audit.WriteAsync(actorId.ToString(), "BOOKING_LOCATION", nameof(Visit), visit.Id.ToString(), null, locationId.ToString());
        await _db.SaveChangesAsync(cancellationToken);
        _cache.InvalidateAll();
    }

    public async Task ApproveAsync(Guid bookingId, Guid actorId, CancellationToken cancellationToken = default)
    {
        var line = await LoadLineAsync(bookingId, cancellationToken);
        await _db.Entry(line).Reference(l => l.Owner).LoadAsync(cancellationToken);
        await _db.Entry(line.Visit).Reference(v => v.Location).LoadAsync(cancellationToken);
        line.Approve();
        line.Visit.RefreshOccupancy();
        await _audit.WriteAsync(actorId.ToString(), "BOOKING_APPROVE", nameof(BookingLine), line.Id.ToString(), BookingStatus.Requested.ToString(), BookingStatus.Confirmed.ToString());
        await _db.SaveChangesAsync(cancellationToken);
        _cache.InvalidateAll();
        if (line.Source == BookingSource.Widget)
            await _notifier.OnConfirmedAsync(line, line.Owner.Email, line.Owner.Name, cancellationToken);
    }

    public async Task CancelAsync(Guid bookingId, Guid actorId, string? reason, CancellationToken cancellationToken = default)
    {
        var line = await LoadLineAsync(bookingId, cancellationToken);
        var before = line.Status.ToString();
        line.Cancel(reason, actorId);
        line.Visit.RecalculateTiming();
        await _audit.WriteAsync(actorId.ToString(), "BOOKING_CANCEL", nameof(BookingLine), line.Id.ToString(), before, BookingStatus.Cancelled.ToString());
        await _db.SaveChangesAsync(cancellationToken);
        _cache.InvalidateAll();
        await _db.Entry(line).Reference(l => l.Owner).LoadAsync(cancellationToken);
        await _notifier.OnCancelledAsync(line, cancellationToken);
    }

    public async Task MarkNoShowAsync(Guid bookingId, Guid actorId, CancellationToken cancellationToken = default)
    {
        var line = await LoadLineAsync(bookingId, cancellationToken);
        line.MarkNoShow();
        line.Visit.RefreshOccupancy();
        await _audit.WriteAsync(actorId.ToString(), "BOOKING_NO_SHOW", nameof(BookingLine), line.Id.ToString(), BookingStatus.Confirmed.ToString(), BookingStatus.NoShow.ToString());
        await _db.SaveChangesAsync(cancellationToken);
        _cache.InvalidateAll();
    }

    public async Task<Guid> CompleteAsync(Guid bookingId, Guid actorId, CancellationToken cancellationToken = default)
    {
        var line = await LoadLineAsync(bookingId, cancellationToken);
        var horse = await _db.Horses.Include(h => h.Owner).FirstAsync(h => h.Id == line.HorseId, cancellationToken);
        var treatment = await _db.TreatmentTypes.FirstOrDefaultAsync(t => t.Id == line.TreatmentTypeId, cancellationToken);
        var performedAt = line.Visit.DeriveLineStart(line);

        var ownerSnapshot = System.Text.Json.JsonSerializer.Serialize(new
        {
            horse.Owner.Name,
            horse.Owner.Email,
            horse.Owner.Phone,
            horse.Owner.AddressStreet,
            horse.Owner.AddressPostcode,
            horse.Owner.AddressCity
        });

        var journal = new JournalEntry(
            horse.Id,
            ownerSnapshot,
            horse.Species.ToString(),
            horse.Sex.ToString(),
            horse.BirthYear.HasValue ? horse.BirthYear.Value.ToString() : horse.AgeGroup,
            horse.Identity,
            performedAt,
            line.ClientNote ?? string.Empty,
            string.Empty,
            string.Empty,
            treatmentTypeId: line.TreatmentTypeId,
            treatmentTypeName: line.TreatmentName,
            durationMinutes: line.DurationMinutes,
            price: line.Price,
            templateDataJson: treatment?.JournalTemplateJson,
            templateVersion: treatment?.JournalTemplateVersion,
            createdBy: actorId,
            bookingLineId: line.Id);

        _db.JournalEntries.Add(journal);
        line.Complete(journal.Id);
        line.Visit.RefreshOccupancy();

        await _audit.WriteAsync(actorId.ToString(), "BOOKING_COMPLETE", nameof(BookingLine), line.Id.ToString(), BookingStatus.Confirmed.ToString(), BookingStatus.Completed.ToString());
        await _audit.WriteAsync(actorId.ToString(), "JOURNAL_CREATE", nameof(JournalEntry), journal.Id.ToString(), null, null);
        await _db.SaveChangesAsync(cancellationToken);
        _cache.InvalidateAll();
        return journal.Id;
    }

    private async Task<Visit> LoadVisitAsync(Guid visitId, CancellationToken cancellationToken)
    {
        return await _db.Visits.Include(v => v.Lines).FirstOrDefaultAsync(v => v.Id == visitId, cancellationToken)
            ?? throw new KeyNotFoundException("Visit not found.");
    }

    private async Task<BookingLine> LoadLineAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        return await _db.BookingLines.Include(l => l.Visit).ThenInclude(v => v.Lines)
            .FirstOrDefaultAsync(l => l.Id == bookingId, cancellationToken)
            ?? throw new KeyNotFoundException("Booking not found.");
    }

    private async Task SaveVisitCatchingOverlap(
        Visit visit,
        Guid treatmentTypeId,
        string? postcode,
        CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsExclusionViolation(ex))
        {
            _db.ChangeTracker.Clear();
            var slots = await FreshSlots(visit.PractitionerId, treatmentTypeId, visit.StartsAt, postcode, cancellationToken);
            throw new BookingConflictException(slots);
        }
    }

    private async Task<IReadOnlyList<Slot>> FreshSlots(
        Guid practitionerId,
        Guid treatmentTypeId,
        DateTimeOffset around,
        string? postcode,
        CancellationToken cancellationToken)
    {
        if (treatmentTypeId == Guid.Empty) return [];
        var local = TimeZoneInfo.ConvertTime(around, TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm"));
        var date = DateOnly.FromDateTime(local.DateTime);
        return await _slots.GetSlotsAsync(
            new SlotQuery(practitionerId, treatmentTypeId, date, date.AddDays(13), postcode),
            new SlotOptions(ApplyMinNotice: false, ApplyMaxAdvance: false),
            cancellationToken);
    }

    private static bool IsExclusionViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.ExclusionViolation;
}

public sealed record BookingLineRequest(
    Guid HorseId,
    Guid TreatmentTypeId,
    string? ClientNote = null,
    string? InternalNote = null);
