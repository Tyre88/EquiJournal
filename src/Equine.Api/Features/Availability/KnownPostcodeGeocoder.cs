using Equine.Domain.Locations;

namespace Equine.Api.Features.Availability;

public sealed class KnownPostcodeGeocoder : IPostcodeGeocoder
{
    public static readonly (double Latitude, double Longitude) Sjobo = (55.6318, 13.7039);
    public static readonly (double Latitude, double Longitude) Lund = (55.7047, 13.1910);
    public static readonly (double Latitude, double Longitude) Tomelilla = (55.5424, 13.9546);

    public Task<(double Latitude, double Longitude)?> GeocodeAsync(
        string postcode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = AddressNormalization.NormalizePostcode(postcode);
        return Task.FromResult<(double Latitude, double Longitude)?>(normalized switch
        {
            "27531" => Sjobo,
            "27350" => Tomelilla,
            "22100" => Lund,
            _ => AddressNormalization.IsValidSwedishPostcode(normalized) ? Sjobo : null
        });
    }
}
