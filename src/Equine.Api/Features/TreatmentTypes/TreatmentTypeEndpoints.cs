using Equine.Domain.Entities;
using Equine.Domain.JournalTemplates;
using Equine.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Equine.Api.Features.TreatmentTypes;

public static class TreatmentTypeEndpoints
{
    public static IEndpointRouteBuilder MapTreatmentTypeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/app/treatment-types").RequireAuthorization();

        group.MapGet("/", async (EquineDbContext db) =>
        {
            var types = await db.TreatmentTypes
                .OrderByDescending(t => t.Name)
                .Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.Slug,
                    t.ShortDescription,
                    t.DurationMinutes,
                    t.PriceExclVat,
                    t.VatRate,
                    t.Status,
                    t.BookableOnline,
                    t.JournalTemplateJson
                })
                .ToListAsync();
            return Results.Ok(types);
        }).WithName("ListTreatmentTypes");

        group.MapGet("/active", async (EquineDbContext db) =>
        {
            var types = await db.TreatmentTypes
                .Where(t => t.Status == Domain.Entities.TreatmentTypeStatus.Active)
                .OrderByDescending(t => t.Name)
                .Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.Slug,
                    t.ShortDescription,
                    t.PublicDescription,
                    t.DurationMinutes,
                    t.PriceExclVat,
                    t.VatRate,
                    t.BookableOnline
                })
                .ToListAsync();
            return Results.Ok(types);
        }).WithName("GetActiveTreatmentTypes");

        group.MapGet("/{id:guid}", async (Guid id, EquineDbContext db) =>
        {
            var type = await db.TreatmentTypes
                .Where(t => t.Id == id)
                .Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.Slug,
                    t.ShortDescription,
                    t.PublicDescription,
                    t.DurationMinutes,
                    t.BufferBeforeMinutes,
                    t.BufferAfterMinutes,
                    t.Colour,
                    t.PriceExclVat,
                    t.VatRate,
                    t.BookableOnline,
                    t.RequiresApproval,
                    t.MinNoticeHours,
                    t.MaxAdvanceDays,
                    t.AllowedLocationTypes,
                    t.FollowUpIntervalDays,
                    t.ShareSummaryWithClient,
                    t.Status,
                    t.JournalTemplateJson,
                    t.JournalTemplateVersion,
                    t.CreatedAt,
                    t.UpdatedAt
                })
                .FirstOrDefaultAsync();

            if (type is null) return Results.NotFound();
            return Results.Ok(type);
        }).WithName("GetTreatmentType");

        group.MapPost("/", async (TreatmentTypeCreateRequest request, EquineDbContext db) =>
        {
            if (!JournalTemplateValidator.TryValidate(request.JournalTemplateJson, out var templateError))
                return Results.BadRequest(templateError);

            var type = new TreatmentType(
                request.Name,
                request.ShortDescription,
                request.PublicDescription,
                request.DurationMinutes,
                request.BufferBeforeMinutes,
                request.BufferAfterMinutes,
                request.Colour,
                request.PriceExclVat,
                request.VatRate,
                request.BookableOnline,
                request.RequiresApproval,
                request.MinNoticeHours,
                request.MaxAdvanceDays,
                request.AllowedLocationTypes,
                request.FollowUpIntervalDays,
                request.JournalTemplateJson,
                request.Slug);

            db.TreatmentTypes.Add(type);
            await db.SaveChangesAsync();

            return Results.Created($"/api/app/treatment-types/{type.Id}", new { type.Id });
        }).WithName("CreateTreatmentType");

        group.MapPut("/{id:guid}", async (Guid id, TreatmentTypeUpdateRequest request, EquineDbContext db) =>
        {
            var type = await db.TreatmentTypes.FindAsync(id);
            if (type is null) return Results.NotFound();

            if (!JournalTemplateValidator.TryValidate(request.JournalTemplateJson, out var templateError))
                return Results.BadRequest(templateError);

            type.Update(
                name: request.Name,
                shortDescription: request.ShortDescription,
                publicDescription: request.PublicDescription,
                durationMinutes: request.DurationMinutes,
                bufferBeforeMinutes: request.BufferBeforeMinutes,
                bufferAfterMinutes: request.BufferAfterMinutes,
                colour: request.Colour,
                priceExclVat: request.PriceExclVat,
                vatRate: request.VatRate,
                bookableOnline: request.BookableOnline,
                requiresApproval: request.RequiresApproval,
                minNoticeHours: request.MinNoticeHours,
                maxAdvanceDays: request.MaxAdvanceDays,
                allowedLocationTypes: request.AllowedLocationTypes,
                followUpIntervalDays: request.FollowUpIntervalDays,
                journalTemplateJson: request.JournalTemplateJson,
                slug: request.Slug,
                shareSummaryWithClient: request.ShareSummaryWithClient);

            await db.SaveChangesAsync();
            return Results.Ok();
        }).WithName("UpdateTreatmentType");

        group.MapPost("/{id:guid}/deactivate", async (Guid id, EquineDbContext db) =>
        {
            var type = await db.TreatmentTypes.FindAsync(id);
            if (type is null) return Results.NotFound();

            type.Deactivate();
            await db.SaveChangesAsync();
            return Results.Ok();
        }).WithName("DeactivateTreatmentType");

        group.MapPost("/{id:guid}/activate", async (Guid id, EquineDbContext db) =>
        {
            var type = await db.TreatmentTypes.FindAsync(id);
            if (type is null) return Results.NotFound();

            type.Activate();
            await db.SaveChangesAsync();
            return Results.Ok();
        }).WithName("ActivateTreatmentType");

        return app;
    }

    public record TreatmentTypeCreateRequest(
        string Name,
        string ShortDescription,
        string? PublicDescription = null,
        int DurationMinutes = 30,
        int BufferBeforeMinutes = 0,
        int BufferAfterMinutes = 0,
        string? Colour = null,
        decimal PriceExclVat = 0,
        decimal VatRate = 0.25m,
        bool BookableOnline = false,
        bool RequiresApproval = false,
        int MinNoticeHours = 0,
        int MaxAdvanceDays = 30,
        string? AllowedLocationTypes = null,
        int? FollowUpIntervalDays = null,
        string? JournalTemplateJson = null,
        string? Slug = null);

    public record TreatmentTypeUpdateRequest(
        string? Name = null,
        string? ShortDescription = null,
        string? PublicDescription = null,
        int? DurationMinutes = null,
        int? BufferBeforeMinutes = null,
        int? BufferAfterMinutes = null,
        string? Colour = null,
        decimal? PriceExclVat = null,
        decimal? VatRate = null,
        bool? BookableOnline = null,
        bool? RequiresApproval = null,
        int? MinNoticeHours = null,
        int? MaxAdvanceDays = null,
        string? AllowedLocationTypes = null,
        int? FollowUpIntervalDays = null,
        string? JournalTemplateJson = null,
        string? Slug = null,
        bool? ShareSummaryWithClient = null);
}
