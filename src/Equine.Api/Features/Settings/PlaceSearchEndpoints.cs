using Equine.Api.Features.Schema;

namespace Equine.Api.Features.Settings;

public static class PlaceSearchEndpoints
{
    public static IEndpointRouteBuilder MapPlaceSearchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/app/places").RequireAuthorization("CanAdminister");

        group.MapGet("/", async (string q, TravelRouteService travel, CancellationToken ct) =>
            Results.Ok(await travel.SearchPlacesAsync(q, ct)));

        group.MapGet("/reverse", async (double lat, double lon, TravelRouteService travel, CancellationToken ct) =>
        {
            var hit = await travel.ReverseAsync(lat, lon, ct);
            return hit is null ? Results.NotFound() : Results.Ok(hit);
        });

        return app;
    }
}
