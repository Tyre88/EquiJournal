using System.Text.Json;
using Equine.Api.Auth;
using Equine.Domain.Entities;
using Equine.Domain.Locations;
using Equine.Domain.Scheduling;
using Equine.Infrastructure;
using Equine.Infrastructure.Scheduling;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Equine.Api.Features.Availability;

public static class AvailabilityEndpoints
{
    public static IEndpointRouteBuilder MapAvailabilityEndpoints(this IEndpointRouteBuilder app)
    {
        var zones = app.MapGroup("/api/app/zones").RequireAuthorization("CanManageBookings");
        var locations = app.MapGroup("/api/app/locations").RequireAuthorization("CanManageBookings");
        var rules = app.MapGroup("/api/app/availability-rules").RequireAuthorization("CanManageBookings");
        var timeOff = app.MapGroup("/api/app/time-off").RequireAuthorization("CanManageBookings");
        var slots = app.MapGroup("/api/app/availability").RequireAuthorization("CanManageBookings");

        zones.MapGet("/", async (EquineDbContext db) =>
        {
            var list = await db.Zones.OrderBy(z => z.IsFallback).ThenBy(z => z.Name).ToListAsync();
            return Results.Ok(list.Select(ToZoneDto));
        }).WithName("ListZones");

        zones.MapGet("/{id:guid}", async (Guid id, EquineDbContext db) =>
        {
            var zone = await db.Zones.FindAsync(id);
            return zone is null ? Results.NotFound() : Results.Ok(ToZoneDto(zone));
        }).WithName("GetZone");

        zones.MapPost("/", async (ZoneRequest request, EquineDbContext db, ISlotCache cache) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest("Namn krävs.");
            try
            {
                var zone = new Zone(request.Name.Trim(), request.TravelBufferMinutes);
                ApplyGeometry(zone, request);
                db.Zones.Add(zone);
                await db.SaveChangesAsync();
                cache.InvalidateAll();
                return Results.Created($"/api/app/zones/{zone.Id}", ToZoneDto(zone));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        }).WithName("CreateZone");

        zones.MapPut("/{id:guid}", async (Guid id, ZoneRequest request, EquineDbContext db, ISlotCache cache) =>
        {
            var zone = await db.Zones.FindAsync(id);
            if (zone is null) return Results.NotFound();
            if (zone.IsFallback)
            {
                zone.Update(travelBufferMinutes: request.TravelBufferMinutes);
            }
            else
            {
                try
                {
                    zone.Update(
                        string.IsNullOrWhiteSpace(request.Name) ? zone.Name : request.Name.Trim(),
                        request.TravelBufferMinutes,
                        request.BufferKm);
                    ApplyGeometry(zone, request, request.BufferKm ?? zone.BufferKm);
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
            }

            await db.SaveChangesAsync();
            cache.InvalidateAll();
            return Results.Ok(ToZoneDto(zone));
        }).WithName("UpdateZone");

        zones.MapDelete("/{id:guid}", async (Guid id, EquineDbContext db, ISlotCache cache) =>
        {
            var zone = await db.Zones.FindAsync(id);
            if (zone is null) return Results.NotFound();
            if (zone.IsFallback) return Results.Conflict("Kan inte ta bort zonen Övrigt.");
            db.Zones.Remove(zone);
            await db.SaveChangesAsync();
            cache.InvalidateAll();
            return Results.Ok();
        }).WithName("DeleteZone");

        locations.MapGet("/", async (EquineDbContext db) =>
            Results.Ok(await db.Locations.OrderBy(l => l.Name).ToListAsync())).WithName("ListLocations");

        locations.MapGet("/{id:guid}", async (Guid id, EquineDbContext db) =>
        {
            var location = await db.Locations.FindAsync(id);
            return location is null ? Results.NotFound() : Results.Ok(location);
        }).WithName("GetLocation");

        locations.MapPost("/", async (LocationRequest request, EquineDbContext db, ISlotCache cache) =>
        {
            var location = new Location(
                request.Type,
                request.Name,
                request.AddressStreet,
                request.AddressPostcode,
                request.AddressCity,
                request.Latitude,
                request.Longitude);
            db.Locations.Add(location);
            await db.SaveChangesAsync();
            cache.InvalidateAll();
            return Results.Created($"/api/app/locations/{location.Id}", location);
        }).WithName("CreateLocation");

        locations.MapPut("/{id:guid}", async (Guid id, LocationRequest request, EquineDbContext db, ISlotCache cache) =>
        {
            var location = await db.Locations.FindAsync(id);
            if (location is null) return Results.NotFound();
            location.Update(request.Name, request.AddressStreet, request.AddressPostcode, request.AddressCity, request.Latitude, request.Longitude);
            await db.SaveChangesAsync();
            cache.InvalidateAll();
            return Results.Ok(location);
        }).WithName("UpdateLocation");

        rules.MapGet("/", async (EquineDbContext db, HttpContext http, [FromQuery] Guid? practitionerId) =>
        {
            var userId = CurrentUser.GetUserId(http);
            var owner = practitionerId ?? userId;
            var query = db.AvailabilityRules.Include(r => r.Zone).AsQueryable();
            if (owner is not null) query = query.Where(r => r.PractitionerId == owner);
            var list = await query.OrderBy(r => r.DayOfWeek).ThenBy(r => r.StartTime).ToListAsync();
            return Results.Ok(list.Select(ToRuleDto));
        }).WithName("ListAvailabilityRules");

        rules.MapPost("/", async (RuleRequest request, HttpContext http, EquineDbContext db, ISlotCache cache) =>
        {
            var userId = CurrentUser.GetUserId(http);
            if (userId is null) return Results.Unauthorized();
            var rule = new AvailabilityRule(
                request.PractitionerId ?? userId.Value,
                request.DayOfWeek,
                request.StartTime,
                request.EndTime,
                request.EffectiveFrom,
                request.EffectiveTo,
                request.ZoneId,
                request.Active ?? true);
            db.AvailabilityRules.Add(rule);
            await db.SaveChangesAsync();
            cache.InvalidateAll();
            return Results.Created($"/api/app/availability-rules/{rule.Id}", ToRuleDto(rule));
        }).WithName("CreateAvailabilityRule");

        rules.MapPut("/{id:guid}", async (Guid id, RuleRequest request, EquineDbContext db, ISlotCache cache) =>
        {
            var rule = await db.AvailabilityRules.FindAsync(id);
            if (rule is null) return Results.NotFound();
            rule.Update(
                request.DayOfWeek,
                request.StartTime,
                request.EndTime,
                request.EffectiveFrom,
                request.EffectiveTo,
                request.ZoneId,
                clearZone: request.ZoneId is null,
                active: request.Active);
            await db.SaveChangesAsync();
            cache.InvalidateAll();
            return Results.Ok(ToRuleDto(rule));
        }).WithName("UpdateAvailabilityRule");

        rules.MapDelete("/{id:guid}", async (Guid id, EquineDbContext db, ISlotCache cache) =>
        {
            var rule = await db.AvailabilityRules.FindAsync(id);
            if (rule is null) return Results.NotFound();
            db.AvailabilityRules.Remove(rule);
            await db.SaveChangesAsync();
            cache.InvalidateAll();
            return Results.Ok();
        }).WithName("DeleteAvailabilityRule");

        timeOff.MapGet("/", async (EquineDbContext db, HttpContext http, [FromQuery] Guid? practitionerId) =>
        {
            var userId = CurrentUser.GetUserId(http);
            var owner = practitionerId ?? userId;
            var query = db.TimeOff.AsQueryable();
            if (owner is not null) query = query.Where(t => t.PractitionerId == owner);
            return Results.Ok(await query.OrderBy(t => t.StartsAt).ToListAsync());
        }).WithName("ListTimeOff");

        timeOff.MapPost("/", async (TimeOffRequest request, HttpContext http, EquineDbContext db, ISlotCache cache) =>
        {
            var userId = CurrentUser.GetUserId(http);
            if (userId is null) return Results.Unauthorized();
            var item = new TimeOff(
                request.PractitionerId ?? userId.Value,
                request.StartsAt.ToUniversalTime(),
                request.EndsAt.ToUniversalTime(),
                request.AllDay,
                request.Reason,
                request.RecurringAnnual);
            db.TimeOff.Add(item);
            await db.SaveChangesAsync();
            cache.InvalidateAll();
            return Results.Created($"/api/app/time-off/{item.Id}", item);
        }).WithName("CreateTimeOff");

        timeOff.MapPut("/{id:guid}", async (Guid id, TimeOffRequest request, EquineDbContext db, ISlotCache cache) =>
        {
            var item = await db.TimeOff.FindAsync(id);
            if (item is null) return Results.NotFound();
            item.Update(request.StartsAt.ToUniversalTime(), request.EndsAt.ToUniversalTime(), request.AllDay, request.Reason, request.RecurringAnnual);
            await db.SaveChangesAsync();
            cache.InvalidateAll();
            return Results.Ok(item);
        }).WithName("UpdateTimeOff");

        timeOff.MapDelete("/{id:guid}", async (Guid id, EquineDbContext db, ISlotCache cache) =>
        {
            var item = await db.TimeOff.FindAsync(id);
            if (item is null) return Results.NotFound();
            db.TimeOff.Remove(item);
            await db.SaveChangesAsync();
            cache.InvalidateAll();
            return Results.Ok();
        }).WithName("DeleteTimeOff");

        slots.MapGet("/slots", async (
            SlotQueryService service,
            HttpContext http,
            [FromQuery] Guid treatmentTypeId,
            [FromQuery] DateOnly from,
            [FromQuery] DateOnly to,
            [FromQuery] Guid? practitionerId = null,
            [FromQuery] string? postcode = null,
            [FromQuery] int? durationMinutes = null,
            [FromQuery] bool applyMinNotice = true) =>
        {
            var userId = CurrentUser.GetUserId(http);
            if (userId is null) return Results.Unauthorized();
            var query = new SlotQuery(practitionerId ?? userId.Value, treatmentTypeId, from, to, postcode);
            var options = new SlotOptions(
                ApplyMinNotice: applyMinNotice,
                ApplyMaxAdvance: applyMinNotice,
                DurationMinutesOverride: durationMinutes);
            var result = await service.GetSlotsAsync(query, options);
            return Results.Ok(result);
        }).WithName("GetAvailabilitySlots");

        return app;
    }

    private static void ApplyGeometry(Zone zone, ZoneRequest request, double? bufferKm = null)
    {
        var kind = request.GeometryKind?.Trim().ToLowerInvariant();
        var buffer = bufferKm ?? request.BufferKm ?? zone.BufferKm;
        if (string.IsNullOrWhiteSpace(kind))
        {
            if (request.ClearGeometry)
                zone.ClearGeometry();
            return;
        }

        if (kind == ZoneGeometryKinds.Circle)
        {
            if (request.Center is null || request.RadiusKm is null)
                throw new ArgumentException("Cirkelzon kräver centrum och radie.");
            zone.SetCircle(request.Center.Lat, request.Center.Lng, request.RadiusKm.Value, buffer);
            return;
        }

        if (kind == ZoneGeometryKinds.Polygon)
        {
            if (request.Geometry is null || request.Geometry.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
                throw new ArgumentException("Yta krävs för en polygonzon.");
            zone.SetPolygon(request.Geometry.Value.GetRawText(), buffer);
            return;
        }

        throw new ArgumentException("Ogiltig zonform.");
    }

    private static object ToZoneDto(Zone z)
    {
        object? geometry = DeserializeGeo(z.GeometryJson);
        object? effective = DeserializeGeo(z.EffectiveGeometryJson);
        return new
        {
            z.Id,
            z.Name,
            z.GeometryKind,
            Geometry = geometry,
            Center = z.CenterLatitude is not null && z.CenterLongitude is not null
                ? new { Lat = z.CenterLatitude, Lng = z.CenterLongitude }
                : null,
            z.RadiusKm,
            z.BufferKm,
            EffectiveGeometry = effective,
            z.TravelBufferMinutes,
            z.IsFallback
        };
    }

    private static object? DeserializeGeo(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    private static object ToRuleDto(AvailabilityRule r) => new
    {
        r.Id,
        r.PractitionerId,
        r.DayOfWeek,
        r.StartTime,
        r.EndTime,
        r.EffectiveFrom,
        r.EffectiveTo,
        r.ZoneId,
        ZoneName = r.Zone?.Name,
        r.Active
    };

    public record GeoPointDto(double Lat, double Lng);
    public record ZoneRequest(
        string Name,
        string? GeometryKind = null,
        JsonElement? Geometry = null,
        GeoPointDto? Center = null,
        double? RadiusKm = null,
        double? BufferKm = null,
        int TravelBufferMinutes = 0,
        bool ClearGeometry = false);
    public record LocationRequest(
        LocationType Type,
        string Name,
        string? AddressStreet = null,
        string? AddressPostcode = null,
        string? AddressCity = null,
        decimal? Latitude = null,
        decimal? Longitude = null);
    public record RuleRequest(
        DayOfWeek DayOfWeek,
        TimeOnly StartTime,
        TimeOnly EndTime,
        DateOnly? EffectiveFrom = null,
        DateOnly? EffectiveTo = null,
        Guid? ZoneId = null,
        Guid? PractitionerId = null,
        bool? Active = true);
    public record TimeOffRequest(
        DateTimeOffset StartsAt,
        DateTimeOffset EndsAt,
        bool AllDay = false,
        string? Reason = null,
        bool RecurringAnnual = false,
        Guid? PractitionerId = null);
}
