using Equine.Domain.Entities;
using Equine.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using JournalStatus = Equine.Domain.Entities.JournalStatus;

namespace Equine.Api.Features.Horses;

public static class HorseEndpoints
{
    public static IEndpointRouteBuilder MapHorseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/app/horses").RequireAuthorization();

        group.MapGet("/", async (EquineDbContext db) =>
        {
            var horses = await db.Horses
                .Include(h => h.Owner)
                .Include(h => h.JournalEntries)
                .Where(h => h.DeletedAt == null)
                .OrderByDescending(h => h.Name)
                .Select(h => new
                {
                    h.Id,
                    h.Name,
                    h.Species,
                    h.Sex,
                    h.BirthYear,
                    h.AgeGroup,
                    h.Status,
                    OwnerName = h.Owner.Name,
                    JournalCount = h.JournalEntries.Count
                })
                .ToListAsync();
            return Results.Ok(horses);
        }).WithName("ListHorses");

        group.MapGet("/by-owner/{ownerId:guid}", async (Guid ownerId, EquineDbContext db) =>
        {
            var horses = await db.Horses
                .Include(h => h.Owner)
                .Where(h => h.OwnerId == ownerId && h.DeletedAt == null)
                .OrderByDescending(h => h.Name)
                .Select(h => new
                {
                    h.Id,
                    h.Name,
                    h.Species,
                    h.Sex,
                    h.BirthYear,
                    h.AgeGroup,
                    h.Status,
                    h.Identity,
                    h.Colour,
                    h.Markings
                })
                .ToListAsync();
            return Results.Ok(horses);
        }).WithName("GetHorsesByOwner");

        group.MapGet("/{id:guid}", async (Guid id, EquineDbContext db) =>
        {
            var horse = await db.Horses
                .Include(h => h.Owner)
                .Include(h => h.JournalEntries)
                .Where(h => h.Id == id && h.DeletedAt == null)
                .Select(h => new
                {
                    h.Id,
                    h.OwnerId,
                    OwnerName = h.Owner.Name,
                    h.Name,
                    h.Species,
                    h.Breed,
                    h.Sex,
                    h.BirthYear,
                    h.AgeGroup,
                    h.Identity,
                    h.Colour,
                    h.Markings,
                    h.StableLocation,
                    h.StableAddress,
                    h.StablePostcode,
                    h.StableCity,
                    h.Background,
                    h.Status,
                    h.CreatedAt,
                    h.UpdatedAt,
                    h.FollowUpOverrideDays,
                    JournalCount = h.JournalEntries.Count,
                    SignedJournalCount = h.JournalEntries.Count(j => j.Status == JournalStatus.Signed)
                })
                .FirstOrDefaultAsync();

            if (horse is null) return Results.NotFound();
            return Results.Ok(horse);
        }).WithName("GetHorse");

        group.MapPost("/", async (HorseCreateRequest request, EquineDbContext db) =>
        {
            var owner = await db.Owners.FindAsync(request.OwnerId);
            if (owner is null) return Results.NotFound("Owner not found");

            var horse = new Horse(
                request.OwnerId,
                request.Name,
                request.Species,
                request.Breed,
                request.Sex,
                request.BirthYear,
                request.AgeGroup,
                request.Identity,
                request.Colour,
                request.Markings,
                request.StableLocation,
                request.StableAddress ?? owner.AddressStreet,
                request.StablePostcode ?? owner.AddressPostcode,
                request.StableCity ?? owner.AddressCity,
                request.StableLatitude ?? owner.Latitude,
                request.StableLongitude ?? owner.Longitude,
                request.Background);

            db.Horses.Add(horse);
            await db.SaveChangesAsync();

            return Results.Created($"/api/app/horses/{horse.Id}", new { horse.Id });
        }).WithName("CreateHorse");

        group.MapPut("/{id:guid}", async (Guid id, HorseUpdateRequest request, EquineDbContext db) =>
        {
            var horse = await db.Horses.FindAsync(id);
            if (horse is null || horse.IsDeleted) return Results.NotFound();

            horse.Update(
                name: request.Name,
                species: request.Species,
                breed: request.Breed,
                sex: request.Sex,
                birthYear: request.BirthYear,
                ageGroup: request.AgeGroup,
                identity: request.Identity,
                colour: request.Colour,
                markings: request.Markings,
                stableLocation: request.StableLocation,
                stableAddress: request.StableAddress,
                stablePostcode: request.StablePostcode,
                stableCity: request.StableCity,
                background: request.Background,
                status: request.Status);
            if (request.FollowUpOverrideDays.HasValue || request.ClearFollowUpOverride == true)
                horse.SetFollowUpOverride(request.ClearFollowUpOverride == true ? null : request.FollowUpOverrideDays);

            await db.SaveChangesAsync();
            return Results.Ok();
        }).WithName("UpdateHorse");

        group.MapDelete("/{id:guid}", async (Guid id, EquineDbContext db) =>
        {
            var horse = await db.Horses
                .Include(h => h.JournalEntries)
                .FirstOrDefaultAsync(h => h.Id == id);

            if (horse is null) return Results.NotFound();

            horse.Archive();
            await db.SaveChangesAsync();
            return Results.Ok();
        }).WithName("ArchiveHorse");

        group.MapPost("/search", async (HorseSearchRequest request, EquineDbContext db) =>
        {
            var query = db.Horses
                .Include(h => h.Owner)
                .Where(h => h.DeletedAt == null)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.Query))
            {
                var q = request.Query.ToLower();
                query = query.Where(h =>
                    EF.Functions.Like(h.Name, $"%{q}%") ||
                    EF.Functions.ILike(h.Owner.Name, $"%{q}%") ||
                    (h.Colour != null && EF.Functions.Like(h.Colour, $"%{q}%")) ||
                    (h.Breed != null && EF.Functions.Like(h.Breed, $"%{q}%")));
            }

            var horses = await query
                .OrderByDescending(h => h.Name)
                .Select(h => new
                {
                    h.Id,
                    h.Name,
                    h.Species,
                    h.Sex,
                    h.BirthYear,
                    h.Status,
                    OwnerName = h.Owner.Name,
                    OwnerEmail = h.Owner.Email
                })
                .Take(50)
                .ToListAsync();

            return Results.Ok(horses);
        }).WithName("SearchHorses");

        return app;
    }

    public record HorseCreateRequest(
        Guid OwnerId,
        string Name,
        Djurslag Species = Djurslag.Hast,
        string? Breed = null,
        HingstSex Sex = HingstSex.Okant,
        int? BirthYear = null,
        string? AgeGroup = null,
        string? Identity = null,
        string? Colour = null,
        string? Markings = null,
        string? StableLocation = null,
        string? StableAddress = null,
        string? StablePostcode = null,
        string? StableCity = null,
        decimal? StableLatitude = null,
        decimal? StableLongitude = null,
        string? Background = null);

    public record HorseUpdateRequest(
        string? Name = null,
        Djurslag? Species = null,
        string? Breed = null,
        HingstSex? Sex = null,
        int? BirthYear = null,
        string? AgeGroup = null,
        string? Identity = null,
        string? Colour = null,
        string? Markings = null,
        string? StableLocation = null,
        string? StableAddress = null,
        string? StablePostcode = null,
        string? StableCity = null,
        string? Background = null,
        string? Status = null,
        int? FollowUpOverrideDays = null,
        bool? ClearFollowUpOverride = null);

    public record HorseSearchRequest(string Query);
}
