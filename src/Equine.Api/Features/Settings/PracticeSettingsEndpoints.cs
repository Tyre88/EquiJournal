using Equine.Api.Features.Schema;
using Equine.Api.Maps;
using Equine.Domain.Entities;
using Equine.Domain.Locations;
using Equine.Infrastructure.Practice;
using Microsoft.Extensions.Options;

namespace Equine.Api.Features.Settings;

public static class PracticeSettingsEndpoints
{
    public static IEndpointRouteBuilder MapPracticeSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/app/practice-settings").RequireAuthorization("CanAdminister");

        group.MapGet("/", async (
            PracticeSettingsService settings,
            IOptions<GoogleMapsOptions> maps,
            CancellationToken ct) =>
        {
            var s = await settings.GetAsync(ct);
            return Results.Ok(ToDto(s, maps.Value.BrowserKey));
        }).WithName("GetPracticeSettings");

        group.MapPut("/", async (
            PracticeSettingsUpdateRequest request,
            PracticeSettingsService settings,
            TravelRouteService travel,
            IOptions<GoogleMapsOptions> maps,
            IHostEnvironment env,
            CancellationToken ct) =>
        {
            var current = await settings.GetAsync(ct);
            var street = request.AddressStreet?.Trim() ?? "";
            var postcode = request.AddressPostcode?.Trim() ?? "";
            var city = request.AddressCity?.Trim() ?? "";
            var composed = AddressNormalization.FormatDisplay(street, postcode, city);
            if (string.IsNullOrWhiteSpace(composed) && !string.IsNullOrWhiteSpace(request.Address))
            {
                composed = request.Address.Trim();
                if (string.IsNullOrWhiteSpace(street))
                    street = composed;
            }

            var previous = AddressNormalization.FormatDisplay(current.AddressStreet, current.AddressPostcode, current.AddressCity);
            if (string.IsNullOrWhiteSpace(previous))
                previous = current.Address?.Trim() ?? "";
            var addressChanged = !string.Equals(previous, composed, StringComparison.OrdinalIgnoreCase);

            decimal? latitude = request.Latitude;
            decimal? longitude = request.Longitude;
            var hasCoords = latitude is not null && longitude is not null;
            if (!hasCoords && !string.IsNullOrWhiteSpace(composed) && !env.IsEnvironment("Testing"))
            {
                var coords = await travel.GeocodeAsync(composed, ct);
                if (coords is null && (!string.IsNullOrWhiteSpace(postcode) || !string.IsNullOrWhiteSpace(city)))
                    coords = await travel.GeocodeAsync(AddressNormalization.FormatDisplay(null, postcode, city), ct);
                if (coords is not null)
                {
                    latitude = (decimal)coords.Value.Lat;
                    longitude = (decimal)coords.Value.Lon;
                }
                else if (addressChanged)
                {
                    latitude = null;
                    longitude = null;
                }
            }

            var s = await settings.UpdateAsync(
                request.Name ?? "",
                request.Clinic ?? "",
                composed,
                street,
                postcode,
                city,
                latitude,
                longitude,
                request.Phone ?? "",
                request.Email ?? "",
                ct);
            return Results.Ok(ToDto(s, maps.Value.BrowserKey));
        }).WithName("UpdatePracticeSettings");

        return app;
    }

    private static object ToDto(PracticeSettings s, string? mapsBrowserKey = null) => new
    {
        s.Name,
        s.Clinic,
        s.Address,
        AddressStreet = string.IsNullOrWhiteSpace(s.AddressStreet) ? s.Address : s.AddressStreet,
        s.AddressPostcode,
        s.AddressCity,
        s.Latitude,
        s.Longitude,
        s.Phone,
        s.Email,
        MapsBrowserKey = mapsBrowserKey ?? ""
    };
}

public sealed record PracticeSettingsUpdateRequest(
    string? Name,
    string? Clinic,
    string? Address,
    string? AddressStreet,
    string? AddressPostcode,
    string? AddressCity,
    decimal? Latitude,
    decimal? Longitude,
    string? Phone,
    string? Email);
