using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Equine.Domain.Scheduling;
using Microsoft.Extensions.Caching.Memory;

namespace Equine.Api.Features.Schema;

public sealed class TravelRouteService
{
    private readonly IHttpClientFactory _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TravelRouteService> _log;

    public TravelRouteService(IHttpClientFactory http, IMemoryCache cache, ILogger<TravelRouteService> log)
    {
        _http = http;
        _cache = cache;
        _log = log;
    }

    public async Task<IReadOnlyList<PlaceSuggestion>> SearchPlacesAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 3)
            return [];

        var key = "places:" + query.Trim().ToLowerInvariant();
        if (_cache.TryGetValue(key, out IReadOnlyList<PlaceSuggestion>? cached) && cached is not null)
            return cached;

        try
        {
            var client = _http.CreateClient("Nominatim");
            var path = $"search?format=json&limit=5&addressdetails=1&countrycodes=se&q={Uri.EscapeDataString(query.Trim())}";
            using var response = await client.GetAsync(path, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return [];

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return [];

            var hits = new List<PlaceSuggestion>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (!TryParseCoord(el, "lat", out var lat) || !TryParseCoord(el, "lon", out var lon))
                    continue;
                var parsed = FromNominatim(el, lat, lon);
                if (parsed is not null) hits.Add(parsed);
            }

            _cache.Set(key, hits, TimeSpan.FromHours(6));
            return hits;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _log.LogWarning(ex, "Place search failed for {Query}", query);
            return [];
        }
    }

    public async Task<PlaceSuggestion?> ReverseAsync(double lat, double lon, CancellationToken cancellationToken)
    {
        var key = string.Create(CultureInfo.InvariantCulture, $"rev:{lat:F5},{lon:F5}");
        if (_cache.TryGetValue(key, out PlaceSuggestion? cached))
            return cached;

        try
        {
            var client = _http.CreateClient("Nominatim");
            var path = string.Create(CultureInfo.InvariantCulture,
                $"reverse?format=json&addressdetails=1&lat={lat:F6}&lon={lon:F6}");
            using var response = await client.GetAsync(path, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            var parsed = FromNominatim(doc.RootElement, lat, lon);
            if (parsed is not null)
                _cache.Set(key, parsed, TimeSpan.FromDays(7));
            return parsed;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _log.LogWarning(ex, "Reverse geocode failed");
            return null;
        }
    }

    public async Task<(double Lat, double Lon)?> GeocodeAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;
        var key = "geocode:" + query.Trim().ToLowerInvariant();
        if (_cache.TryGetValue(key, out (double Lat, double Lon)? cached))
            return cached;

        try
        {
            var client = _http.CreateClient("Nominatim");
            var path = $"search?format=json&limit=1&countrycodes=se&q={Uri.EscapeDataString(query.Trim())}";
            using var response = await client.GetAsync(path, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning("Nominatim {Status} for {Query}", response.StatusCode, query);
                return Cache(key, null);
            }

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                return Cache(key, null);

            var first = doc.RootElement[0];
            if (!TryParseCoord(first, "lat", out var lat) || !TryParseCoord(first, "lon", out var lon))
                return Cache(key, null);

            return Cache(key, (lat, lon));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _log.LogWarning(ex, "Geocode failed for {Query}", query);
            return null;
        }
    }

    public async Task<(double DistanceKm, int DurationMinutes)?> RouteAsync(
        double fromLat, double fromLon, double toLat, double toLon, CancellationToken cancellationToken)
    {
        var key = string.Create(CultureInfo.InvariantCulture,
            $"route:{fromLat:F4},{fromLon:F4}:{toLat:F4},{toLon:F4}");
        if (_cache.TryGetValue(key, out (double DistanceKm, int DurationMinutes)? cached) && cached is not null)
            return cached;

        try
        {
            var client = _http.CreateClient("Osrm");
            var path = string.Create(CultureInfo.InvariantCulture,
                $"route/v1/driving/{fromLon:F6},{fromLat:F6};{toLon:F6},{toLat:F6}?overview=false");
            var payload = await client.GetFromJsonAsync<OsrmResponse>(path, cancellationToken);
            var route = payload?.Routes?.FirstOrDefault();
            if (route is not null)
            {
                var km = Math.Round(route.Distance / 1000d, 1);
                var minutes = Math.Max(1, (int)Math.Round(route.Duration / 60d));
                var result = (km, minutes);
                _cache.Set(key, result, TimeSpan.FromHours(24));
                return result;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            _log.LogWarning(ex, "OSRM route failed, falling back to estimate");
        }

        var estimateKm = Math.Round(SchemaTravel.HaversineKm(fromLat, fromLon, toLat, toLon) * 1.25, 1);
        var fallback = (estimateKm, SchemaTravel.EstimateDriveMinutes(estimateKm));
        _cache.Set(key, fallback, TimeSpan.FromHours(6));
        return fallback;
    }

    private (double Lat, double Lon)? Cache(string key, (double Lat, double Lon)? value)
    {
        _cache.Set(key, value, TimeSpan.FromDays(30));
        return value;
    }

    private static PlaceSuggestion? FromNominatim(JsonElement el, double lat, double lon)
    {
        var label = el.TryGetProperty("display_name", out var display) ? display.GetString() ?? "" : "";
        var street = "";
        var postcode = "";
        var city = "";
        if (el.TryGetProperty("address", out var address) && address.ValueKind == JsonValueKind.Object)
        {
            var road = Get(address, "road", "pedestrian", "footway");
            var number = Get(address, "house_number");
            street = string.Join(" ", new[] { road, number }.Where(v => v.Length > 0));
            postcode = Get(address, "postcode");
            city = Get(address, "city", "town", "village", "municipality");
        }
        if (street.Length == 0 && label.Length == 0) return null;
        return new PlaceSuggestion(label, street, postcode, city, Math.Round(lat, 7), Math.Round(lon, 7));
    }

    private static string Get(JsonElement address, params string[] names)
    {
        foreach (var name in names)
        {
            if (address.TryGetProperty(name, out var prop))
            {
                var value = prop.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
        }
        return "";
    }

    private static bool TryParseCoord(JsonElement el, string name, out double value)
    {
        value = 0;
        if (!el.TryGetProperty(name, out var prop)) return false;
        return double.TryParse(prop.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    public sealed record PlaceSuggestion(string Label, string Street, string Postcode, string City, double Latitude, double Longitude);

    private sealed class OsrmResponse
    {
        public List<OsrmRoute>? Routes { get; set; }
    }

    private sealed class OsrmRoute
    {
        public double Distance { get; set; }
        public double Duration { get; set; }
    }
}
