namespace Equine.Domain.Locations;

public interface IPostcodeGeocoder
{
    Task<(double Latitude, double Longitude)?> GeocodeAsync(
        string postcode,
        CancellationToken cancellationToken = default);
}
