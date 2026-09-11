using Equine.Domain.Entities;
using Equine.Domain.Scheduling;
using Equine.Infrastructure;
using Equine.Infrastructure.Practice;
using Microsoft.EntityFrameworkCore;

namespace Equine.Api.Features.Schema;

public sealed class SchemaDayService
{
    private static readonly TimeZoneInfo Stockholm = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");

    private readonly EquineDbContext _db;
    private readonly TravelRouteService _travel;
    private readonly PracticeSettingsService _practice;
    private readonly SchemaPlaceResolver _places;

    public SchemaDayService(
        EquineDbContext db,
        TravelRouteService travel,
        PracticeSettingsService practice,
        SchemaPlaceResolver places)
    {
        _db = db;
        _travel = travel;
        _practice = practice;
        _places = places;
    }

    public async Task<SchemaDayDto> GetAsync(DateOnly? date, CancellationToken cancellationToken)
    {
        var day = date ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Stockholm).DateTime);
        var startLocal = day.ToDateTime(TimeOnly.MinValue);
        var start = new DateTimeOffset(startLocal, Stockholm.GetUtcOffset(startLocal)).ToUniversalTime();
        var end = start.AddDays(1);

        var lines = await _db.BookingLines
            .Include(l => l.Visit).ThenInclude(v => v.Location)
            .Include(l => l.Horse)
            .Include(l => l.Owner)
            .Include(l => l.TreatmentType)
            .Where(l => l.Visit.EndsAt > start && l.Visit.StartsAt < end && l.Status != BookingStatus.Cancelled)
            .ToListAsync(cancellationToken);

        var ordered = lines.OrderBy(l => l.Visit.DeriveLineStart(l)).ToList();
        var home = await ResolveHomeAsync(cancellationToken);

        BookingLine? previous = null;
        (double Lat, double Lon)? previousCoords = home.Coords;
        string? previousPlace = "home";
        DateTimeOffset? previousEndsAt = null;
        var locationsDirty = false;
        var stops = new List<SchemaStopDto>();

        foreach (var line in ordered)
        {
            var location = line.Visit.Location;
            var coords = await _places.ResolveStopCoordsAsync(location, line, cancellationToken);
            if (location is not null && coords is not null && (location.Latitude is null || location.Longitude is null))
            {
                location.Update(
                    location.Name,
                    location.AddressStreet,
                    location.AddressPostcode,
                    location.AddressCity,
                    (decimal)coords.Value.Lat,
                    (decimal)coords.Value.Lon);
                locationsDirty = true;
            }

            var placeKey = SchemaPlaces.PlaceKey(location, coords);
            var starts = line.Visit.DeriveLineStart(line);
            var ends = line.Visit.DeriveLineEnd(line);
            var origin = SchemaTravel.Origin(previousEndsAt, previousPlace, starts, placeKey);
            var samePlace = previous is not null && SchemaTravel.SamePlace(previousPlace, placeKey);

            TravelLegDto? travel = null;
            if (samePlace)
            {
                travel = new TravelLegDto("previous", previous is null ? "Hemmet" : PreviousLabel(previous), true, 0, 0);
            }
            else
            {
                var from = origin == TravelOrigin.Home ? home.Coords : previousCoords;
                var fromLabel = origin == TravelOrigin.Home ? "Hemmet" : PreviousLabel(previous);
                if (from is not null && coords is not null)
                {
                    var route = await _travel.RouteAsync(
                        from.Value.Lat, from.Value.Lon, coords.Value.Lat, coords.Value.Lon, cancellationToken);
                    travel = route is null
                        ? new TravelLegDto(origin == TravelOrigin.Home ? "home" : "previous", fromLabel, false, null, null)
                        : new TravelLegDto(
                            origin == TravelOrigin.Home ? "home" : "previous",
                            fromLabel,
                            false,
                            route.Value.DistanceKm,
                            route.Value.DurationMinutes);
                }
                else
                {
                    travel = new TravelLegDto(origin == TravelOrigin.Home ? "home" : "previous", fromLabel, false, null, null);
                }
            }

            stops.Add(ToStop(line, starts, ends, coords, travel));
            previous = line;
            previousCoords = coords ?? previousCoords;
            previousPlace = placeKey ?? previousPlace;
            previousEndsAt = ends;
        }

        if (locationsDirty)
            await _db.SaveChangesAsync(cancellationToken);

        return new SchemaDayDto(home.Dto, stops);
    }

    private async Task<HomeResolution> ResolveHomeAsync(CancellationToken cancellationToken)
    {
        var practitioner = await _practice.GetAsync(cancellationToken);
        var home = await _places.ResolveHomeAsync(practitioner, cancellationToken);
        return new HomeResolution(
            new SchemaHomeDto(home.Name, home.Address, home.Coords?.Lat, home.Coords?.Lon),
            home.Coords);
    }

    private static string PreviousLabel(BookingLine? previous)
    {
        if (previous is null) return "Hemmet";
        var name = previous.Visit.Location?.Name;
        if (!string.IsNullOrWhiteSpace(name)) return name;
        return previous.Owner?.Name ?? "Föregående bokning";
    }

    private static SchemaStopDto ToStop(
        BookingLine line,
        DateTimeOffset starts,
        DateTimeOffset ends,
        (double Lat, double Lon)? coords,
        TravelLegDto? travel)
    {
        var location = line.Visit.Location;
        return new SchemaStopDto(
            line.Id,
            line.Visit.Id,
            starts,
            ends,
            line.Status.ToString(),
            line.HorseId,
            line.Horse?.Name,
            line.OwnerId,
            line.Owner?.Name,
            line.Owner?.Phone,
            line.TreatmentTypeId,
            line.TreatmentName,
            line.TreatmentType?.Colour,
            line.DurationMinutes,
            line.ClientNote,
            line.JournalEntryId,
            location?.Id,
            location?.Name,
            location?.AddressStreet,
            location?.AddressPostcode,
            location?.AddressCity,
            coords?.Lat ?? (double?)location?.Latitude,
            coords?.Lon ?? (double?)location?.Longitude,
            travel);
    }

    private readonly record struct HomeResolution(SchemaHomeDto Dto, (double Lat, double Lon)? Coords);
}

public sealed record SchemaDayDto(SchemaHomeDto Home, IReadOnlyList<SchemaStopDto> Stops);

public sealed record SchemaHomeDto(string Name, string Address, double? Latitude, double? Longitude);

public sealed record TravelLegDto(string From, string FromLabel, bool SamePlace, double? DistanceKm, int? DurationMinutes);

public sealed record SchemaStopDto(
    Guid Id,
    Guid VisitId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Status,
    Guid HorseId,
    string? HorseName,
    Guid OwnerId,
    string? OwnerName,
    string? OwnerPhone,
    Guid TreatmentTypeId,
    string TreatmentName,
    string? TreatmentColour,
    int DurationMinutes,
    string? ClientNote,
    Guid? JournalEntryId,
    Guid? LocationId,
    string? LocationName,
    string? AddressStreet,
    string? AddressPostcode,
    string? AddressCity,
    double? Latitude,
    double? Longitude,
    TravelLegDto? Travel);
