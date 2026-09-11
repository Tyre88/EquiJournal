using Equine.Domain.Entities;
using Equine.Domain.Locations;
using Equine.Domain.Scheduling;

namespace Equine.Api.Features.Schema;

public sealed class SchemaPlaceResolver
{
    private readonly TravelRouteService _travel;

    public SchemaPlaceResolver(TravelRouteService travel) => _travel = travel;

    public async Task<ResolvedHome> ResolveHomeAsync(PracticeSettings practice, CancellationToken cancellationToken)
    {
        (double Lat, double Lon)? coords = null;
        if (practice.Latitude is not null && practice.Longitude is not null)
            coords = ((double)practice.Latitude.Value, (double)practice.Longitude.Value);
        else
        {
            foreach (var query in SchemaPlaces.HomeQueries(practice))
            {
                coords = await _travel.GeocodeAsync(query, cancellationToken);
                if (coords is not null) break;
            }
        }

        var address = string.IsNullOrWhiteSpace(practice.Address)
            ? AddressNormalization.FormatDisplay(practice.AddressStreet, practice.AddressPostcode, practice.AddressCity)
            : practice.Address;

        return new ResolvedHome(practice.Name, address, coords);
    }

    public async Task<(double Lat, double Lon)?> ResolveStopCoordsAsync(
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

        foreach (var query in SchemaPlaces.AddressQueries(location, line))
        {
            var coords = await _travel.GeocodeAsync(query, cancellationToken);
            if (coords is not null) return coords;
        }

        return null;
    }
}

public readonly record struct ResolvedHome(string Name, string Address, (double Lat, double Lon)? Coords);
