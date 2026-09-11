using Equine.Api.Auth;
using Equine.Domain.Entities;
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
            var zone = new Zone(request.Name, request.Postcodes, request.TravelBufferMinutes);
            db.Zones.Add(zone);
            await db.SaveChangesAsync();
            cache.InvalidateAll();
            return Results.Created($"/api/app/zones/{zone.Id}", ToZoneDto(zone));
        }).WithName("CreateZone");

        zones.MapPut("/{id:guid}", async (Guid id, ZoneRequest request, EquineDbContext db, ISlotCache cache) =>
        {
            var zone = await db.Zones.FindAsync(id);
            if (zone is null) return Results.NotFound();
            if (zone.IsFallback)
                zone.Update(travelBufferMinutes: request.TravelBufferMinutes);
            else
                zone.Update(request.Name, request.Postcodes, request.TravelBufferMinutes);
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

    private static object ToZoneDto(Zone z) => new
    {
        z.Id,
        z.Name,
        Postcodes = z.GetPostcodes(),
        z.TravelBufferMinutes,
        z.IsFallback
    };

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

    public record ZoneRequest(string Name, IReadOnlyList<string>? Postcodes = null, int TravelBufferMinutes = 0);
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
