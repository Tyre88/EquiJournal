using Equine.Domain.Entities;
using Equine.Domain.Locations;
using Equine.Domain.Scheduling;
using Equine.Infrastructure;
using Equine.Infrastructure.Practice;
using Equine.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Equine.Api.Features.Schema;

public sealed class SchemaDayService
{
    private static readonly TimeZoneInfo Stockholm = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");

    private readonly EquineDbContext _db;
    private readonly TravelRouteService _travel;
    private readonly PracticeSettingsService _practice;

    public SchemaDayService(
        EquineDbContext db,
        TravelRouteService travel,
        PracticeSettingsService practice)
    {
        _db = db;
        _travel = travel;
        _practice = practice;
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
            var coords = await ResolveStopCoordsAsync(location, line, cancellationToken);
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

            var placeKey = PlaceKey(location, coords);
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
        (double Lat, double Lon)? coords = null;
        if (practitioner.Latitude is not null && practitioner.Longitude is not null)
            coords = ((double)practitioner.Latitude.Value, (double)practitioner.Longitude.Value);
        else
        {
            foreach (var query in HomeQueries(practitioner))
            {
                coords = await _travel.GeocodeAsync(query, cancellationToken);
                if (coords is not null) break;
            }
        }

        var address = string.IsNullOrWhiteSpace(practitioner.Address)
            ? AddressNormalization.FormatDisplay(practitioner.AddressStreet, practitioner.AddressPostcode, practitioner.AddressCity)
            : practitioner.Address;

        return new HomeResolution(
            new SchemaHomeDto(practitioner.Name, address, coords?.Lat, coords?.Lon),
            coords);
    }

    private static IEnumerable<string> HomeQueries(PracticeSettings practice)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var query in new[]
        {
            AddressNormalization.FormatDisplay(practice.AddressStreet, practice.AddressPostcode, practice.AddressCity),
            practice.Address,
            AddressNormalization.FormatDisplay(null, practice.AddressPostcode, practice.AddressCity),
            practice.AddressCity
        })
        {
            var trimmed = query?.Trim() ?? "";
            if (trimmed.Length == 0 || !seen.Add(trimmed)) continue;
            yield return trimmed;
        }
    }

    private async Task<(double Lat, double Lon)?> ResolveStopCoordsAsync(
        Location? location,
        BookingLine line,
        CancellationToken cancellationToken)
    {
        if (location?.Latitude is not null && location.Longitude is not null)
            return ((double)location.Latitude.Value, (double)location.Longitude.Value);
        if (line.Horse?.StableLatitude is not null && line.Horse.StableLongitude is not null)
            return ((double)line.Horse.StableLatitude.Value, (double)line.Horse.StableLongitude.Value);
        if (line.Owner?.Latitude is not null && line.Owner.Longitude is not null)
            return ((double)line.Owner.Latitude.Value, (double)line.Owner.Longitude.Value);

        foreach (var query in AddressQueries(location, line))
        {
            var coords = await _travel.GeocodeAsync(query, cancellationToken);
            if (coords is not null) return coords;
        }

        return null;
    }

    private static IEnumerable<string> AddressQueries(Location? location, BookingLine line)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var query in CandidateQueries(location, line))
        {
            var trimmed = query.Trim();
            if (trimmed.Length == 0 || !seen.Add(trimmed)) continue;
            yield return trimmed;
        }
    }

    private static IEnumerable<string> CandidateQueries(Location? location, BookingLine line)
    {
        var places = new (string? Street, string? Postcode, string? City)[]
        {
            (location?.AddressStreet, location?.AddressPostcode, location?.AddressCity),
            (line.Horse?.StableAddress, line.Horse?.StablePostcode, line.Horse?.StableCity),
            (line.Owner?.AddressStreet, line.Owner?.AddressPostcode, line.Owner?.AddressCity)
        };

        foreach (var (street, postcode, city) in places)
        {
            var full = JoinAddress(street, postcode, city);
            if (!string.IsNullOrWhiteSpace(full)) yield return full;
        }

        foreach (var (_, postcode, city) in places)
        {
            var town = JoinAddress(postcode, city);
            if (!string.IsNullOrWhiteSpace(town)) yield return town;
            if (!string.IsNullOrWhiteSpace(city)) yield return city!;
        }

        if (!string.IsNullOrWhiteSpace(location?.Name) && location.Name.Length > 3)
        {
            yield return location.Name;
            var namedTown = JoinAddress(location.Name, location.AddressCity);
            if (!string.IsNullOrWhiteSpace(namedTown)) yield return namedTown;
        }
    }

    private static string JoinAddress(params string?[] parts) =>
        string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));

    private static string? PlaceKey(Location? location, (double Lat, double Lon)? coords)
    {
        if (location is not null && !string.IsNullOrWhiteSpace(location.NormalizedKey) && !location.NormalizedKey.StartsWith("id:", StringComparison.Ordinal))
            return location.NormalizedKey;
        if (location?.Id is { } id)
            return "loc:" + id.ToString("N");
        if (coords is not null)
            return $"{coords.Value.Lat:F4},{coords.Value.Lon:F4}";
        return null;
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
