using Equine.Api.Features.Schema;
using Equine.Domain.Entities;
using Equine.Domain.Scheduling;
using Equine.Infrastructure;
using Equine.Infrastructure.Practice;
using Microsoft.EntityFrameworkCore;

namespace Equine.Api.Features.Reports;

public sealed class DriveLogService
{
    public const string Disclaimer =
        "Körsträckor är beräknade via vägdata, inte mätarställning. Endast tjänsteresor från bokningar. Privata resor ingår inte.";

    private static readonly TimeZoneInfo Stockholm = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");

    private readonly EquineDbContext _db;
    private readonly PracticeSettingsService _practice;
    private readonly SchemaPlaceResolver _places;
    private readonly TravelRouteService _travel;

    public DriveLogService(
        EquineDbContext db,
        PracticeSettingsService practice,
        SchemaPlaceResolver places,
        TravelRouteService travel)
    {
        _db = db;
        _practice = practice;
        _places = places;
        _travel = travel;
    }

    public async Task<DriveLogReport> GetAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var start = ToUtcStart(from);
        var end = ToUtcEnd(to);
        var settings = await _practice.GetAsync(ct);
        var homeResolved = await _places.ResolveHomeAsync(settings, ct);
        var home = new DriveLogHome(homeResolved.Address, homeResolved.Coords?.Lat, homeResolved.Coords?.Lon);

        var lines = await _db.BookingLines
            .Include(l => l.Visit).ThenInclude(v => v.Location)
            .Include(l => l.Visit).ThenInclude(v => v.Lines)
            .Include(l => l.Horse)
            .Include(l => l.Owner)
            .Include(l => l.TreatmentType)
            .Where(l => l.Visit.EndsAt > start && l.Visit.StartsAt < end && l.Status != BookingStatus.Cancelled)
            .ToListAsync(ct);

        var drafts = new List<DriveLogTripDraft>();
        foreach (var dayGroup in lines.GroupBy(l => LocalDate(l.Visit.DeriveLineStart(l))).OrderBy(g => g.Key))
        {
            var stops = new List<DriveLogStopInput>();
            foreach (var line in dayGroup.OrderBy(l => l.Visit.DeriveLineStart(l)))
            {
                var location = line.Visit.Location;
                var coords = await _places.ResolveStopCoordsAsync(location, line, ct);
                var placeKey = SchemaPlaces.PlaceKey(location, coords) ?? "line:" + line.Id.ToString("N");
                stops.Add(new DriveLogStopInput(
                    line.Visit.DeriveLineStart(line),
                    line.Visit.DeriveLineEnd(line),
                    placeKey,
                    SchemaPlaces.StopName(location, line),
                    SchemaPlaces.StopAddress(location, line),
                    line.Owner?.Name,
                    line.Horse?.Name,
                    line.TreatmentName,
                    coords?.Lat,
                    coords?.Lon));
            }

            drafts.AddRange(DriveLogPlanner.BuildDay(dayGroup.Key, home, stops));
        }

        var trips = new List<DriveLogTripRow>();
        foreach (var draft in drafts)
        {
            var km = await DistanceKmAsync(draft, ct);
            if (km is 0) continue;
            trips.Add(ToRow(draft, settings, km));
        }

        var totalKm = trips.Where(t => t.DistanceKm is not null).Sum(t => t.DistanceKm!.Value);
        var meta = new DriveLogMeta(
            settings.Name,
            settings.Clinic,
            settings.VehicleRegistrationNumber,
            from,
            to,
            trips.Count,
            Math.Round(totalKm, 1),
            Disclaimer);

        return new DriveLogReport(meta, trips);
    }

    private async Task<double?> DistanceKmAsync(DriveLogTripDraft draft, CancellationToken ct)
    {
        if (draft.FromLatitude is null || draft.FromLongitude is null
            || draft.ToLatitude is null || draft.ToLongitude is null)
            return null;

        var route = await _travel.RouteAsync(
            draft.FromLatitude.Value,
            draft.FromLongitude.Value,
            draft.ToLatitude.Value,
            draft.ToLongitude.Value,
            ct);
        return route?.DistanceKm;
    }

    private static DriveLogTripRow ToRow(DriveLogTripDraft draft, PracticeSettings settings, double? km) =>
        new(
            draft.Date,
            TimeZoneInfo.ConvertTime(draft.StartsAt, Stockholm),
            draft.FromAddress,
            draft.ToAddress,
            km,
            draft.Purpose == DriveLogPurpose.ReturnHome ? "Hemresa" : "Tjänsteresa",
            draft.Purpose == DriveLogPurpose.ReturnHome ? SchemaPlaces.HomeLabel : draft.ToLabel,
            draft.Contacts,
            draft.Horses,
            draft.Treatments,
            settings.Name,
            settings.VehicleRegistrationNumber);

    private static DateOnly LocalDate(DateTimeOffset value) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(value, Stockholm).DateTime);

    private static DateTimeOffset ToUtcStart(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, Stockholm.GetUtcOffset(local)).ToUniversalTime();
    }

    private static DateTimeOffset ToUtcEnd(DateOnly date) => ToUtcStart(date.AddDays(1));
}

public sealed record DriveLogReport(DriveLogMeta Meta, IReadOnlyList<DriveLogTripRow> Trips);

public sealed record DriveLogMeta(
    string DriverName,
    string Clinic,
    string VehicleRegistrationNumber,
    DateOnly From,
    DateOnly To,
    int TripCount,
    double TotalKm,
    string Disclaimer);

public sealed record DriveLogTripRow(
    DateOnly Date,
    DateTimeOffset StartsAt,
    string FromAddress,
    string ToAddress,
    double? DistanceKm,
    string Purpose,
    string PlaceVisited,
    string Contacts,
    string Horses,
    string Treatments,
    string DriverName,
    string VehicleRegistrationNumber);
