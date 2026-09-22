using Equine.Domain.Entities;
using Equine.Infrastructure;
using Equine.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using JournalStatus = Equine.Domain.Entities.JournalStatus;

namespace Equine.Api.Features.Owners;

public static class OwnerEndpoints
{
    public static IEndpointRouteBuilder MapOwnerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/app/owners").RequireAuthorization("CanManageBookings");

        group.MapGet("/", async (EquineDbContext db) =>
        {
            var owners = await db.Owners
                .OrderByDescending(o => o.Name)
                .Select(o => new { o.Id, o.Name, o.Email, o.Phone, o.AddressCity })
                .ToListAsync();
            return Results.Ok(owners);
        }).WithName("ListOwners");

        group.MapGet("/{id:guid}", async (Guid id, EquineDbContext db) =>
        {
            var owner = await db.Owners
                .Where(o => o.Id == id && o.DeletedAt == null)
                .Select(o => new
                {
                    o.Id,
                    o.Name,
                    o.Email,
                    o.Phone,
                    o.AddressStreet,
                    o.AddressPostcode,
                    o.AddressCity,
                    o.Latitude,
                    o.Longitude,
                    o.Notes,
                    o.MarketingConsent,
                    o.MarketingConsentAt,
                    o.CreatedAt,
                    o.UpdatedAt
                })
                .FirstOrDefaultAsync();

            if (owner is null) return Results.NotFound();
            return Results.Ok(owner);
        }).WithName("GetOwner");

        group.MapPost("/", async (OwnerCreateRequest request, EquineDbContext db) =>
        {
            var owner = new Owner(
                request.Name,
                request.Email,
                request.Phone,
                request.AddressStreet,
                request.AddressPostcode,
                request.AddressCity,
                request.Latitude,
                request.Longitude,
                request.Notes,
                request.MarketingConsent);

            db.Owners.Add(owner);
            await db.SaveChangesAsync();

            return Results.Created($"/api/app/owners/{owner.Id}", new { owner.Id });
        }).WithName("CreateOwner");

        group.MapPut("/{id:guid}", async (Guid id, OwnerUpdateRequest request, EquineDbContext db) =>
        {
            var owner = await db.Owners.FindAsync(id);
            if (owner is null || owner.IsDeleted) return Results.NotFound();

            owner.Update(
                name: request.Name,
                email: request.Email,
                phone: request.Phone,
                addressStreet: request.AddressStreet,
                addressPostcode: request.AddressPostcode,
                addressCity: request.AddressCity,
                notes: request.Notes,
                marketingConsent: request.MarketingConsent,
                latitude: request.Latitude,
                longitude: request.Longitude);

            await db.SaveChangesAsync();
            return Results.Ok();
        }).WithName("UpdateOwner");

        group.MapPost("/{id:guid}/restore", async (Guid id, EquineDbContext db, ITenantContext tenant) =>
        {
            var owner = await db.Owners
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Id == id && o.TenantId == tenant.TenantId);

            if (owner is null) return Results.NotFound();
            if (!owner.IsDeleted) return Results.Conflict("Owner is not archived.");

            owner.Restore();
            await db.SaveChangesAsync();
            return Results.Ok();
        }).WithName("RestoreOwner");

        group.MapDelete("/{id:guid}", async (Guid id, EquineDbContext db) =>
        {
            var owner = await db.Owners
                .Include(o => o.Horses)
                .ThenInclude(h => h.JournalEntries)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (owner is null) return Results.NotFound();

            var hasHorsesWithJournals = owner.Horses.Any(h => h.JournalEntries.Any(j => j.Status == JournalStatus.Signed));
            if (hasHorsesWithJournals)
            {
                return Results.Conflict("Cannot delete owner with horses that have signed journal entries. Archive the horses first.");
            }

            owner.SoftDelete();
            await db.SaveChangesAsync();
            return Results.Ok();
        }).WithName("DeleteOwner");

        group.MapPost("/search", async (OwnerSearchRequest request, EquineDbContext db) =>
        {
            var query = db.Owners
                .Where(o => o.DeletedAt == null)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.Query))
            {
                var q = request.Query.ToLower();
                query = query.Where(o =>
                    EF.Functions.Like(o.Name, $"%{q}%") ||
                    EF.Functions.Like(o.Email, $"%{q}%") ||
                    (o.Phone != null && EF.Functions.Like(o.Phone, $"%{q}%")) ||
                    EF.Functions.ILike(o.Name, $"%{q}%") ||
                    EF.Functions.ILike(o.Email, $"%{q}%"));
            }

            var owners = await query
                .OrderByDescending(o => o.Name)
                .Select(o => new
                {
                    o.Id,
                    o.Name,
                    o.Email,
                    o.Phone,
                    o.AddressCity
                })
                .Take(50)
                .ToListAsync();

            return Results.Ok(owners);
        }).WithName("SearchOwners");

        return app;
    }

    public record OwnerCreateRequest(
        string Name,
        string Email,
        string? Phone = null,
        string? AddressStreet = null,
        string? AddressPostcode = null,
        string? AddressCity = null,
        decimal? Latitude = null,
        decimal? Longitude = null,
        string? Notes = null,
        bool MarketingConsent = false);

    public record OwnerUpdateRequest(
        string? Name = null,
        string? Email = null,
        string? Phone = null,
        string? AddressStreet = null,
        string? AddressPostcode = null,
        string? AddressCity = null,
        string? Notes = null,
        bool? MarketingConsent = null,
        decimal? Latitude = null,
        decimal? Longitude = null);

    public record OwnerSearchRequest(string Query);
}
