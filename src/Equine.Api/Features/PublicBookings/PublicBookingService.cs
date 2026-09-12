using System.Security.Cryptography;
using System.Text;
using Equine.Api.Features.Availability;
using Equine.Api.Features.Bookings;
using Equine.Domain.Common;
using Equine.Domain.Entities;
using Equine.Domain.Locations;
using Equine.Domain.Scheduling;
using Equine.Infrastructure;
using Equine.Infrastructure.Audit;
using Equine.Infrastructure.Email;
using Equine.Infrastructure.Scheduling;
using Equine.Infrastructure.Tokens;
using Equine.Infrastructure.Tenancy;
using Equine.Infrastructure.Widget;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Equine.Api.Features.PublicBookings;

public sealed class PublicBookingService
{
    public static readonly object TokenNotFound = new { };
    private const int MinFormSeconds = 3;
    private const int MaxSlotRangeDays = 14;

    private readonly EquineDbContext _db;
    private readonly SlotQueryService _slots;
    private readonly LocationResolver _locations;
    private readonly ISlotCache _cache;
    private readonly IAuditWriter _audit;
    private readonly IBookingMailer _mailer;
    private readonly PublicBookingTokenService _tokens;
    private readonly WidgetSettingsService _settings;
    private readonly PractitionerResolver _practitioner;
    private readonly ITenantContext _tenant;
    private readonly ILogger<PublicBookingService> _log;

    public PublicBookingService(
        EquineDbContext db,
        SlotQueryService slots,
        LocationResolver locations,
        ISlotCache cache,
        IAuditWriter audit,
        IBookingMailer mailer,
        PublicBookingTokenService tokens,
        WidgetSettingsService settings,
        PractitionerResolver practitioner,
        ITenantContext tenant,
        ILogger<PublicBookingService> log)
    {
        _db = db;
        _slots = slots;
        _locations = locations;
        _cache = cache;
        _audit = audit;
        _mailer = mailer;
        _tokens = tokens;
        _settings = settings;
        _practitioner = practitioner;
        _tenant = tenant;
        _log = log;
    }

    public async Task<object> ListTreatmentsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settings.GetAsync(cancellationToken);
        var types = await _db.TreatmentTypes.AsNoTracking()
            .Where(t => t.Status == TreatmentTypeStatus.Active && t.BookableOnline)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        var items = types.Select(t => settings.ShowPrices
            ? (object)new
            {
                t.Id,
                t.Slug,
                t.Name,
                t.PublicDescription,
                t.DurationMinutes,
                t.RequiresApproval,
                Price = t.PriceExclVat
            }
            : new
            {
                t.Id,
                t.Slug,
                t.Name,
                t.PublicDescription,
                t.DurationMinutes,
                t.RequiresApproval
            }).ToList();

        return new
        {
            treatments = items,
            settings.BookingTerms,
            settings.PrivacyPolicyUrl,
            settings.ContactPhone,
            settings.ContactEmail,
            settings.CancellationNoticeHours
        };
    }

    public async Task<IReadOnlyList<DateTimeOffset>> GetSlotStartsAsync(
        Guid treatmentId,
        DateOnly from,
        DateOnly to,
        string postcode,
        CancellationToken cancellationToken = default)
    {
        if (to < from) (from, to) = (to, from);
        if (to.DayNumber - from.DayNumber > MaxSlotRangeDays)
            to = from.AddDays(MaxSlotRangeDays);

        var treatment = await RequireOnlineTreatment(treatmentId, cancellationToken);
        var practitionerId = await _practitioner.GetIdAsync(cancellationToken);
        var slots = await _slots.GetSlotsAsync(
            new SlotQuery(practitionerId, treatment.Id, from, to, AddressNormalization.NormalizePostcode(postcode)),
            new SlotOptions(ApplyMinNotice: true, ApplyMaxAdvance: true),
            cancellationToken);
        return slots.Select(s => s.StartsAt).ToList();
    }

    public async Task<string> CreateAsync(
        PublicCreateBookingRequest request,
        string? ip,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            LogAttempt("honeypot", ip, request.TreatmentId, request.Owner.Email);
            return PublicBookingReference.Create();
        }

        if (request.FormOpenedAt is null
            || request.FormOpenedAt > DateTimeOffset.UtcNow.AddMinutes(1)
            || DateTimeOffset.UtcNow - request.FormOpenedAt.Value < TimeSpan.FromSeconds(MinFormSeconds))
        {
            LogAttempt("too-fast", ip, request.TreatmentId, request.Owner.Email);
            throw new ArgumentException("Kunde inte slutföra bokningen.");
        }

        if (!request.Consent)
            throw new ArgumentException("Samtycke krävs.");
        if (!AddressNormalization.IsValidSwedishPostcode(request.Owner.AddressPostcode))
            throw new ArgumentException("Ogiltigt postnummer.");
        if (string.IsNullOrWhiteSpace(request.Horse.Name) || string.IsNullOrWhiteSpace(request.Owner.Name)
            || string.IsNullOrWhiteSpace(request.Owner.Email) || string.IsNullOrWhiteSpace(request.Owner.Phone)
            || string.IsNullOrWhiteSpace(request.Owner.AddressStreet))
            throw new ArgumentException("Alla obligatoriska fält måste fyllas i.");
        if (request.Horse.BirthYear is null && string.IsNullOrWhiteSpace(request.Horse.AgeGroup))
            throw new ArgumentException("Ange ålder eller åldersgrupp.");

        var treatment = await RequireOnlineTreatment(request.TreatmentId, cancellationToken);
        var practitionerId = await _practitioner.GetIdAsync(cancellationToken);
        var postcode = AddressNormalization.NormalizePostcode(request.Owner.AddressPostcode);
        var email = request.Owner.Email.Trim();

        var owner = await _db.Owners.FirstOrDefaultAsync(o => o.Email == email, cancellationToken);
        if (owner is null)
        {
            owner = new Owner(
                request.Owner.Name.Trim(),
                email,
                request.Owner.Phone.Trim(),
                request.Owner.AddressStreet.Trim(),
                postcode,
                request.Owner.AddressCity?.Trim(),
                emailVerified: false);
            _db.Owners.Add(owner);
        }

        var horseName = request.Horse.Name.Trim();
        var horse = await _db.Horses.FirstOrDefaultAsync(
            h => h.OwnerId == owner.Id && h.Name.ToLower() == horseName.ToLower(),
            cancellationToken);
        if (horse is null)
        {
            horse = new Horse(
                owner.Id,
                horseName,
                breed: request.Horse.Breed?.Trim(),
                birthYear: request.Horse.BirthYear,
                ageGroup: request.Horse.AgeGroup?.Trim(),
                stableAddress: request.Owner.AddressStreet.Trim(),
                stablePostcode: postcode,
                stableCity: request.Owner.AddressCity?.Trim(),
                background: request.Horse.KnownIssues?.Trim(),
                emailVerified: false);
            _db.Horses.Add(horse);
        }

        var location = await _locations.ResolveForHorseAsync(horse, owner, cancellationToken);
        var visit = new Visit(practitionerId, location.Id, request.StartsAt.ToUniversalTime(), BookingSource.Widget);
        _db.Visits.Add(visit);

        var reference = PublicBookingReference.Create();
        var line = new BookingLine(
            visit.Id,
            horse.Id,
            owner.Id,
            treatment.Id,
            treatment.Name,
            treatment.DurationMinutes,
            treatment.PriceExclVat,
            treatment.VatRate,
            BookingStatus.Requested,
            BookingSource.Widget,
            0,
            request.Note,
            publicReference: reference);
        visit.AddLine(line);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsExclusionViolation(ex))
        {
            _db.ChangeTracker.Clear();
            var starts = await GetSlotStartsAsync(treatment.Id, Around(request.StartsAt), Around(request.StartsAt).AddDays(13), postcode, cancellationToken);
            LogAttempt("conflict", ip, treatment.Id, email);
            throw new BookingConflictException(starts.Select(s => new Slot(s, s)).ToList());
        }

        await _audit.WriteAsync("public", "PUBLIC_BOOKING_CREATE", nameof(BookingLine), line.Id.ToString(), null, reference, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        _cache.InvalidateAll();

        await _mailer.SendVerificationAsync(line, email, owner.Name, cancellationToken);
        LogAttempt("created", ip, treatment.Id, email);
        return reference;
    }

    public async Task VerifyAsync(string token, string? ip, CancellationToken cancellationToken = default)
    {
        var line = await ResolveLineAsync(token, PublicBookingTokenService.VerifyPurpose, cancellationToken);
        if (line is null)
        {
            LogAttempt("verify-unknown", ip, null, null);
            throw new KeyNotFoundException("Not found.");
        }

        if (line.EmailVerifiedAt is not null)
            return;

        if (line.Status != BookingStatus.Requested)
            throw new KeyNotFoundException("Not found.");

        line.MarkEmailVerified();
        line.Owner.MarkEmailVerified();
        line.Horse.MarkEmailVerified();

        var treatment = line.TreatmentType;
        if (!treatment.RequiresApproval)
        {
            line.Approve();
            line.Visit.RefreshOccupancy();
            await _mailer.SendConfirmationAsync(line, line.Owner.Email, line.Owner.Name, cancellationToken);
        }
        else
        {
            await _mailer.SendPractitionerApprovalAsync(line, cancellationToken);
        }

        await _audit.WriteAsync("public", "PUBLIC_BOOKING_VERIFY", nameof(BookingLine), line.Id.ToString(), null, line.Status.ToString(), cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        _cache.InvalidateAll();
        LogAttempt("verified", ip, line.TreatmentTypeId, line.Owner.Email);
    }

    public async Task<object?> GetSummaryAsync(string token, CancellationToken cancellationToken = default)
    {
        var line = await ResolveLineAsync(token, PublicBookingTokenService.ManagePurpose, cancellationToken);
        if (line is null) return null;

        var settings = await _settings.GetAsync(cancellationToken);
        var starts = line.Visit.DeriveLineStart(line);
        var ends = line.Visit.DeriveLineEnd(line);
        var withinNotice = starts - DateTimeOffset.UtcNow < TimeSpan.FromHours(settings.CancellationNoticeHours);
        return new
        {
            line.PublicReference,
            line.TreatmentName,
            StartsAt = starts,
            EndsAt = ends,
            line.Status,
            AddressPostcode = line.Visit.Location?.AddressPostcode,
            AddressCity = line.Visit.Location?.AddressCity,
            CancellationPolicy = withinNotice
                ? "Avbokning så nära inpå tiden kan omfattas av avbokningsvillkoren."
                : null
        };
    }

    public async Task CancelAsync(string token, string? reason, CancellationToken cancellationToken = default)
    {
        var line = await ResolveLineAsync(token, PublicBookingTokenService.ManagePurpose, cancellationToken)
            ?? throw new KeyNotFoundException("Not found.");
        line.Cancel(reason, null);
        line.Visit.RecalculateTiming();
        await _audit.WriteAsync("public", "PUBLIC_BOOKING_CANCEL", nameof(BookingLine), line.Id.ToString(), null, reason, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        _cache.InvalidateAll();
        await _mailer.SendCancelledAsync(line, cancellationToken);
    }

    public async Task RescheduleAsync(string token, DateTimeOffset startsAt, CancellationToken cancellationToken = default)
    {
        var line = await ResolveLineAsync(token, PublicBookingTokenService.ManagePurpose, cancellationToken)
            ?? throw new KeyNotFoundException("Not found.");
        if (line.Status is not (BookingStatus.Requested or BookingStatus.Confirmed))
            throw new KeyNotFoundException("Not found.");

        var visit = line.Visit;
        visit.Reschedule(startsAt.ToUniversalTime());
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsExclusionViolation(ex))
        {
            _db.ChangeTracker.Clear();
            var postcode = visit.Location?.AddressPostcode;
            var starts = await GetSlotStartsAsync(line.TreatmentTypeId, Around(startsAt), Around(startsAt).AddDays(13), postcode ?? "", cancellationToken);
            throw new BookingConflictException(starts.Select(s => new Slot(s, s)).ToList());
        }

        await _audit.WriteAsync("public", "PUBLIC_BOOKING_RESCHEDULE", nameof(Visit), visit.Id.ToString(), null, visit.StartsAt.ToString("o"), cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        _cache.InvalidateAll();
        await _db.Entry(line).Reference(l => l.Owner).LoadAsync(cancellationToken);
        await _db.Entry(line).Reference(l => l.Horse).LoadAsync(cancellationToken);
        await _mailer.SendRescheduledAsync(line, line.Owner.Email, line.Owner.Name, cancellationToken);
    }

    public async Task NotifyApprovedAsync(BookingLine line, CancellationToken cancellationToken = default)
    {
        if (line.Source != BookingSource.Widget) return;
        var owner = line.Owner ?? await _db.Owners.FirstAsync(o => o.Id == line.OwnerId, cancellationToken);
        await _mailer.SendConfirmationAsync(line, owner.Email, owner.Name, cancellationToken);
    }

    private async Task<BookingLine?> ResolveLineAsync(string token, string purpose, CancellationToken cancellationToken)
    {
        if (!_tokens.TryUnprotect(token, purpose, out var lineId, out var expired) || expired)
            return null;

        var line = await _db.BookingLines
            .IgnoreQueryFilters()
            .Include(l => l.Visit).ThenInclude(v => v.Location)
            .Include(l => l.Visit).ThenInclude(v => v.Lines)
            .Include(l => l.Owner)
            .Include(l => l.Horse)
            .Include(l => l.TreatmentType)
            .FirstOrDefaultAsync(l => l.Id == lineId && l.Source == BookingSource.Widget, cancellationToken);
        if (line is not null)
            _tenant.SetTenant(line.TenantId);
        return line;
    }

    private async Task<TreatmentType> RequireOnlineTreatment(Guid treatmentId, CancellationToken cancellationToken)
    {
        var treatment = await _db.TreatmentTypes.FirstOrDefaultAsync(t => t.Id == treatmentId, cancellationToken)
            ?? throw new ArgumentException("Behandlingen finns inte.");
        if (!treatment.BookableOnline || treatment.Status != TreatmentTypeStatus.Active)
            throw new ArgumentException("Behandlingen kan inte bokas online.");
        return treatment;
    }

    private static DateOnly Around(DateTimeOffset at)
    {
        var local = TimeZoneInfo.ConvertTime(at, TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm"));
        return DateOnly.FromDateTime(local.DateTime);
    }

    private static bool IsExclusionViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.ExclusionViolation;

    private void LogAttempt(string outcome, string? ip, Guid? treatmentId, string? email)
    {
        _log.LogInformation(
            "PublicBookingAttempt Outcome={Outcome} Ip={Ip} TreatmentId={TreatmentId} EmailHash={EmailHash}",
            outcome,
            ip ?? "unknown",
            treatmentId,
            HashEmail(email));
    }

    private static string HashEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));
        return Convert.ToHexString(bytes)[..12];
    }
}
