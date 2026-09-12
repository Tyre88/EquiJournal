using Equine.Api.Features.Schema;
using Equine.Domain.Locations;

namespace Equine.Api.Features.Availability;

public sealed class NominatimPostcodeGeocoder : IPostcodeGeocoder
{
    private readonly TravelRouteService _routes;

    public NominatimPostcodeGeocoder(TravelRouteService routes) => _routes = routes;

    public async Task<(double Latitude, double Longitude)?> GeocodeAsync(
        string postcode,
        CancellationToken cancellationToken = default)
    {
        var normalized = AddressNormalization.NormalizePostcode(postcode);
        if (!AddressNormalization.IsValidSwedishPostcode(normalized))
            return null;

        var formatted = AddressNormalization.FormatSwedishPostcode(normalized);
        var hit = await _routes.GeocodeAsync($"{formatted}, Sverige", cancellationToken);
        return hit is null ? null : (hit.Value.Lat, hit.Value.Lon);
    }
}
