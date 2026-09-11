using Equine.Api.Auth;
using Equine.Domain.Entities;
using Equine.Infrastructure;
using Equine.Infrastructure.Audit;
using Equine.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Equine.Api.Features.Journals;

public static class JournalEndpoints
{
    public static IEndpointRouteBuilder MapJournalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/app/journals").RequireAuthorization("CanReadJournals");

        group.MapGet("/drafts", async (
            EquineDbContext db,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25) =>
        {
            (page, pageSize) = NormalizePage(page, pageSize);
            var query = db.JournalEntries
                .Where(j => j.Status == JournalStatus.Draft)
                .OrderBy(j => j.CreatedAt);

            var total = await query.CountAsync();
            var drafts = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(j => new
                {
                    j.Id,
                    j.HorseId,
                    HorseName = j.Horse.Name,
                    OwnerName = j.Horse.Owner.Name,
                    j.PerformedAt,
                    j.CreatedAt,
                    j.UpdatedAt,
                    j.Status,
                    j.CreatedBy
                })
                .ToListAsync();

            return Results.Ok(new { Drafts = drafts, Total = total, Page = page, PageSize = pageSize });
        }).WithName("GetDrafts");

        group.MapGet("/", async (
            EquineDbContext db,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25) =>
        {
            (page, pageSize) = NormalizePage(page, pageSize);
            var query = db.JournalEntries
                .OrderByDescending(j => j.PerformedAt);

            var total = await query.CountAsync();
            var journals = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(j => new
                {
                    j.Id,
                    j.HorseId,
                    HorseName = j.Horse.Name,
                    OwnerName = j.Horse.Owner.Name,
                    j.Species,
                    j.PerformedAt,
                    j.Status,
                    j.SignedAt,
                    j.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(new { Journals = journals, Total = total, Page = page, PageSize = pageSize });
        }).WithName("ListJournals");

        group.MapPost("/export/pdf/{id:guid}", async (
            Guid id,
            EquineDbContext db,
            IPdfExportService pdf) =>
        {
            var journal = await db.JournalEntries
                .Include(j => j.Amendments)
                .Include(j => j.Attachments)
                .Include(j => j.Horse)
                .ThenInclude(h => h.Owner)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (journal is null) return Results.NotFound();

            var pdfBytes = await pdf.ExportJournalEntryAsync(journal);
            return Results.File(pdfBytes, "application/pdf", $"journal_{id}.pdf");
        }).WithName("ExportJournalPdf");

        group.MapPost("/export/pdf-horse/{horseId:guid}", async (
            Guid horseId,
            EquineDbContext db,
            IPdfExportService pdf) =>
        {
            var horse = await db.Horses
                .Include(h => h.JournalEntries)
                .ThenInclude(j => j.Amendments)
                .Include(h => h.JournalEntries)
                .ThenInclude(j => j.Attachments)
                .Include(h => h.Owner)
                .FirstOrDefaultAsync(h => h.Id == horseId);

            if (horse is null) return Results.NotFound();

            var pdfBytes = await pdf.ExportHorseJournalHistoryAsync(horse);
            return Results.File(pdfBytes, "application/pdf", $"journal_history_{horse.Name}.pdf");
        }).WithName("ExportHorseJournalPdf");

        group.MapGet("/by-horse/{horseId:guid}", async (
            Guid horseId,
            EquineDbContext db,
            [FromQuery] DateTimeOffset? startDate = null,
            [FromQuery] DateTimeOffset? endDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25) =>
        {
            (page, pageSize) = NormalizePage(page, pageSize);
            IQueryable<JournalEntry> query = db.JournalEntries.Where(j => j.HorseId == horseId);

            if (startDate.HasValue)
                query = query.Where(j => j.PerformedAt >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(j => j.PerformedAt <= endDate.Value);

            query = query.OrderByDescending(j => j.PerformedAt);

            var total = await query.CountAsync();
            var journals = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(j => new
                {
                    j.Id,
                    j.HorseId,
                    j.Species,
                    j.Sex,
                    j.AgeOrAgeGroup,
                    j.AnimalIdentity,
                    j.PerformedAt,
                    j.Anamnes,
                    j.StatusKlinisk,
                    j.Atgarder,
                    j.Diagnos,
                    j.Status,
                    j.SignedAt,
                    j.SignedBy,
                    j.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(new { Journals = journals, Total = total, Page = page, PageSize = pageSize });
        }).WithName("GetJournalsByHorse");

        group.MapPost("/search", async (
            JournalSearchRequest request,
            EquineDbContext db) =>
        {
            List<Guid> ids;
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                ids = await db.JournalEntries
                    .OrderByDescending(j => j.PerformedAt)
                    .Take(50)
                    .Select(j => j.Id)
                    .ToListAsync();
            }
            else
            {
                var q = request.Query.Trim();
                ids = await db.Database.SqlQuery<Guid>($@"
                    SELECT ""Id"" AS ""Value""
                    FROM journal_entries
                    WHERE ""search_vector"" @@ plainto_tsquery('swedish', {q})
                    ORDER BY ""PerformedAt"" DESC
                    LIMIT 50").ToListAsync();
            }

            var journals = await db.JournalEntries
                .Where(j => ids.Contains(j.Id))
                .OrderByDescending(j => j.PerformedAt)
                .Select(j => new
                {
                    j.Id,
                    j.HorseId,
                    HorseName = j.Horse.Name,
                    OwnerName = j.Horse.Owner.Name,
                    j.Species,
                    j.Sex,
                    j.AgeOrAgeGroup,
                    j.PerformedAt,
                    j.Anamnes,
                    j.StatusKlinisk,
                    j.Status,
                    j.SignedAt
                })
                .ToListAsync();

            return Results.Ok(journals);
        }).WithName("SearchJournals");

        group.MapGet("/{id:guid}", async (
            Guid id,
            HttpContext http,
            EquineDbContext db,
            IAuditWriter auditWriter) =>
        {
            var journal = await db.JournalEntries
                .Where(j => j.Id == id)
                .Select(j => new
                {
                    j.Id,
                    j.HorseId,
                    HorseName = j.Horse.Name,
                    OwnerName = j.Horse.Owner.Name,
                    OwnerSnapshot = j.OwnerSnapshot,
                    j.Species,
                    j.Sex,
                    j.AgeOrAgeGroup,
                    j.AnimalIdentity,
                    j.PerformedAt,
                    j.Anamnes,
                    j.StatusKlinisk,
                    j.Atgarder,
                    j.Diagnos,
                    j.Differentialdiagnoser,
                    j.PrognosOchPlan,
                    j.TreatmentTypeId,
                    j.TreatmentTypeName,
                    j.DurationMinutes,
                    j.Price,
                    j.TemplateDataJson,
                    j.TemplateVersion,
                    j.Status,
                    j.SignedAt,
                    j.SignedBy,
                    j.ContentHash,
                    j.CreatedAt,
                    j.UpdatedAt,
                    j.CreatedBy,
                    Amendments = j.Amendments.OrderBy(a => a.CreatedAt).Select(a => new
                    {
                        a.Id,
                        a.Text,
                        a.Reason,
                        a.CreatedAt,
                        a.CreatedBy
                    }),
                    Attachments = j.Attachments.Select(a => new
                    {
                        a.Id,
                        a.StorageKey,
                        a.OriginalFileName,
                        a.ContentType,
                        a.Size,
                        a.Sha256Checksum,
                        a.CreatedAt
                    })
                })
                .FirstOrDefaultAsync();

            if (journal is null) return Results.NotFound();

            await auditWriter.WriteAsync(
                CurrentUser.GetActorId(http),
                "JOURNAL_READ",
                nameof(JournalEntry),
                id.ToString(),
                null,
                null);
            await db.SaveChangesAsync();

            return Results.Ok(journal);
        }).WithName("GetJournal");

        group.MapPost("/", async (
            JournalCreateRequest request,
            HttpContext http,
            EquineDbContext db,
            IAuditWriter auditWriter) =>
        {
            var userId = CurrentUser.GetUserId(http);
            if (userId is null) return Results.Unauthorized();

            var horse = await db.Horses.FindAsync(request.HorseId);
            if (horse is null) return Results.NotFound("Horse not found");

            var owner = await db.Owners.FindAsync(horse.OwnerId);
            var ownerSnapshot = System.Text.Json.JsonSerializer.Serialize(new
            {
                owner?.Name,
                owner?.Email,
                owner?.Phone,
                owner?.AddressStreet,
                owner?.AddressPostcode,
                owner?.AddressCity
            });

            string? treatmentName = request.TreatmentTypeName;
            int? duration = request.DurationMinutes;
            decimal? price = request.Price;
            int templateVersion = request.TemplateVersion ?? 1;

            if (request.TreatmentTypeId is Guid treatmentId)
            {
                var treatment = await db.TreatmentTypes.FindAsync(treatmentId);
                if (treatment is not null)
                {
                    treatmentName = treatment.Name;
                    duration ??= treatment.DurationMinutes;
                    price ??= treatment.PriceExclVat;
                    templateVersion = treatment.JournalTemplateVersion;
                }
            }

            var species = string.IsNullOrWhiteSpace(request.Species)
                ? horse.Species.ToString()
                : request.Species;

            var journal = new JournalEntry(
                request.HorseId,
                ownerSnapshot,
                species,
                horse.Sex.ToString(),
                horse.BirthYear.HasValue ? horse.BirthYear.Value.ToString() : horse.AgeGroup,
                horse.Identity,
                request.PerformedAt == default ? DateTimeOffset.UtcNow : request.PerformedAt,
                request.Anamnes ?? string.Empty,
                request.StatusKlinisk ?? string.Empty,
                request.Atgarder ?? string.Empty,
                request.Diagnos,
                request.Differentialdiagnoser,
                request.PrognosOchPlan,
                request.TreatmentTypeId,
                treatmentName,
                duration,
                price,
                request.TemplateDataJson,
                templateVersion,
                userId.Value);

            db.JournalEntries.Add(journal);
            await auditWriter.WriteAsync(
                userId.Value.ToString(),
                "JOURNAL_CREATE",
                nameof(JournalEntry),
                journal.Id.ToString(),
                null,
                null);
            await db.SaveChangesAsync();

            return Results.Created($"/api/app/journals/{journal.Id}", new
            {
                journal.Id,
                journal.HorseId,
                journal.Status,
                journal.PerformedAt,
                journal.CreatedAt,
                journal.UpdatedAt
            });
        }).RequireAuthorization("CanWriteJournals").WithName("CreateJournal");

        group.MapPut("/{id:guid}", async (
            Guid id,
            JournalUpdateRequest request,
            EquineDbContext db) =>
        {
            var journal = await db.JournalEntries.FindAsync(id);
            if (journal is null) return Results.NotFound();
            if (journal.Status == JournalStatus.Signed)
                return Results.Conflict("Cannot update a signed journal entry.");

            journal.UpdateDraft(
                anamnes: request.Anamnes ?? string.Empty,
                statusKlinisk: request.StatusKlinisk ?? string.Empty,
                atgarder: request.Atgarder ?? string.Empty,
                diagnos: request.Diagnos,
                differentialdiagnoser: request.Differentialdiagnoser,
                prognosOchPlan: request.PrognosOchPlan,
                templateDataJson: request.TemplateDataJson,
                durationMinutes: request.DurationMinutes,
                price: request.Price);

            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization("CanWriteJournals").WithName("UpdateJournal");

        group.MapDelete("/{id:guid}", async (Guid id, EquineDbContext db) =>
        {
            var journal = await db.JournalEntries.FindAsync(id);
            if (journal is null) return Results.NotFound();
            if (journal.Status == JournalStatus.Signed)
                return Results.Conflict("Cannot delete a signed journal entry.");

            db.JournalEntries.Remove(journal);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("CanWriteJournals").WithName("DeleteDraftJournal");

        group.MapPost("/{id:guid}/sign", async (
            Guid id,
            HttpContext http,
            EquineDbContext db,
            IAuditWriter auditWriter) =>
        {
            var journal = await db.JournalEntries.FirstOrDefaultAsync(j => j.Id == id);
            if (journal is null) return Results.NotFound();
            if (journal.Status == JournalStatus.Signed)
                return Results.Conflict("Journal is already signed.");

            if (!journal.ValidateForSigning())
                return Results.BadRequest("Cannot sign: required fields are missing.");

            var signedBy = CurrentUser.GetDisplayName(http);
            journal.Sign(signedBy);
            await auditWriter.WriteAsync(
                CurrentUser.GetActorId(http),
                "JOURNAL_SIGN",
                nameof(JournalEntry),
                journal.Id.ToString(),
                null,
                journal.ContentHash);
            await db.SaveChangesAsync();

            return Results.Ok(new { journal.Id, journal.SignedAt, journal.SignedBy, journal.ContentHash });
        }).RequireAuthorization("CanWriteJournals").WithName("SignJournal");

        group.MapPost("/{id:guid}/amendments", async (
            Guid id,
            JournalAmendmentRequest request,
            HttpContext http,
            EquineDbContext db,
            IAuditWriter auditWriter) =>
        {
            var userId = CurrentUser.GetUserId(http);
            if (userId is null) return Results.Unauthorized();

            var journal = await db.JournalEntries.FindAsync(id);
            if (journal is null) return Results.NotFound();
            if (journal.Status != JournalStatus.Signed)
                return Results.BadRequest("Can only add amendments to signed journals.");

            var amendment = new JournalAmendment(
                id,
                request.Text,
                request.Reason,
                userId.Value);

            db.JournalAmendments.Add(amendment);
            await auditWriter.WriteAsync(
                userId.Value.ToString(),
                "JOURNAL_AMEND",
                nameof(JournalEntry),
                id.ToString(),
                null,
                request.Text);
            await db.SaveChangesAsync();

            return Results.Created($"/api/app/journals/{id}/amendments/{amendment.Id}", new
            {
                amendment.Id,
                amendment.Text,
                amendment.Reason,
                amendment.CreatedAt,
                amendment.CreatedBy
            });
        }).RequireAuthorization("CanWriteJournals").WithName("AddJournalAmendment");

        return app;
    }

    private static (int Page, int PageSize) NormalizePage(int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 25;
        if (pageSize > 100) pageSize = 100;
        return (page, pageSize);
    }

    public record JournalCreateRequest(
        Guid HorseId,
        DateTimeOffset PerformedAt,
        string? Species = null,
        string? Anamnes = null,
        string? StatusKlinisk = null,
        string? Atgarder = null,
        string? Diagnos = null,
        string? Differentialdiagnoser = null,
        string? PrognosOchPlan = null,
        Guid? TreatmentTypeId = null,
        string? TreatmentTypeName = null,
        int? DurationMinutes = null,
        decimal? Price = null,
        string? TemplateDataJson = null,
        int? TemplateVersion = null);

    public record JournalUpdateRequest(
        string? Anamnes = null,
        string? StatusKlinisk = null,
        string? Atgarder = null,
        string? Diagnos = null,
        string? Differentialdiagnoser = null,
        string? PrognosOchPlan = null,
        string? TemplateDataJson = null,
        int? DurationMinutes = null,
        decimal? Price = null);

    public record JournalSearchRequest(string Query);

    public record JournalAmendmentRequest(string Text, string Reason);
}
