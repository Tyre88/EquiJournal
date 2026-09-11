using Equine.Api.Auth;
using Equine.Domain.Entities;
using Equine.Infrastructure;
using Equine.Infrastructure.Audit;
using Equine.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Equine.Api.Features.Attachments;

public static class AttachmentEndpoints
{
    private static readonly TimeSpan SignedUrlLifetime = TimeSpan.FromMinutes(15);

    public static IEndpointRouteBuilder MapAttachmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/app/journals/{journalId:guid}/attachments")
            .RequireAuthorization("CanReadJournals");

        group.MapGet("/", async (
            Guid journalId,
            EquineDbContext db) =>
        {
            var attachments = await db.Attachments
                .Where(a => a.JournalEntryId == journalId)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new
                {
                    a.Id,
                    a.StorageKey,
                    a.OriginalFileName,
                    a.ContentType,
                    a.Size,
                    a.Sha256Checksum,
                    a.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(attachments);
        }).WithName("ListAttachments");

        group.MapPost("/", async (
            Guid journalId,
            IFormFile file,
            HttpContext http,
            EquineDbContext db,
            IObjectStorage storage,
            IAuditWriter auditWriter) =>
        {
            var userId = CurrentUser.GetUserId(http);
            if (userId is null) return Results.Unauthorized();

            if (file is null || file.Length == 0)
                return Results.BadRequest("No file provided.");

            var journal = await db.JournalEntries.FindAsync(journalId);
            if (journal is null) return Results.NotFound("Journal entry not found.");

            var maxFileSize = 50L * 1024 * 1024;
            if (file.Length > maxFileSize)
                return Results.BadRequest("File too large (max 50MB).");

            await using var stream = file.OpenReadStream();
            var headerBytes = new byte[12];
            await stream.ReadExactlyAsync(headerBytes.AsMemory(0, (int)Math.Min(12, file.Length)));
            stream.Position = 0;

            var contentType = ValidateMagicBytes(headerBytes, file.ContentType);
            if (contentType is null)
                return Results.BadRequest("Invalid file content type (magic bytes check failed).");

            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = await sha256.ComputeHashAsync(stream);
            stream.Position = 0;
            var checksum = Convert.ToHexStringLower(hashBytes);

            var safeName = Path.GetFileName(file.FileName);
            var storageKey = $"journals/{journalId}/{Guid.CreateVersion7()}_{safeName}";

            await storage.PutAsync(storageKey, stream, contentType);

            var attachment = new Attachment(
                journalId,
                storageKey,
                safeName,
                contentType,
                file.Length,
                checksum,
                userId.Value);

            db.Attachments.Add(attachment);
            await auditWriter.WriteAsync(
                userId.Value.ToString(),
                "ATTACHMENT_UPLOAD",
                nameof(Attachment),
                attachment.Id.ToString(),
                null,
                storageKey);
            await db.SaveChangesAsync();

            var expiresAt = DateTimeOffset.UtcNow.Add(SignedUrlLifetime);
            var signedUrl = storage.GetPresignedUrl(storageKey, SignedUrlLifetime);

            return Results.Ok(new
            {
                attachment.Id,
                attachment.StorageKey,
                attachment.OriginalFileName,
                attachment.ContentType,
                attachment.Size,
                attachment.Sha256Checksum,
                SignedUrl = signedUrl,
                ExpiresAt = expiresAt
            });
        }).RequireAuthorization("CanWriteJournals").DisableAntiforgery().WithName("UploadAttachment");

        group.MapGet("/{attachmentId:guid}/download", async (
            Guid journalId,
            Guid attachmentId,
            EquineDbContext db,
            IObjectStorage storage) =>
        {
            var attachment = await db.Attachments
                .FirstOrDefaultAsync(a => a.Id == attachmentId && a.JournalEntryId == journalId);

            if (attachment is null) return Results.NotFound();

            var expiresAt = DateTimeOffset.UtcNow.Add(SignedUrlLifetime);
            var signedUrl = storage.GetPresignedUrl(attachment.StorageKey, SignedUrlLifetime);

            return Results.Ok(new
            {
                attachment.StorageKey,
                attachment.OriginalFileName,
                attachment.ContentType,
                attachment.Size,
                attachment.Sha256Checksum,
                SignedUrl = signedUrl,
                ExpiresAt = expiresAt
            });
        }).WithName("GetAttachmentUrl");

        group.MapDelete("/{attachmentId:guid}", async (
            Guid journalId,
            Guid attachmentId,
            EquineDbContext db) =>
        {
            var journal = await db.JournalEntries.FindAsync(journalId);
            if (journal is null) return Results.NotFound();
            if (journal.Status == JournalStatus.Signed)
                return Results.Conflict("Cannot delete attachments on a signed journal.");

            var attachment = await db.Attachments
                .FirstOrDefaultAsync(a => a.Id == attachmentId && a.JournalEntryId == journalId);
            if (attachment is null) return Results.NotFound();

            db.Attachments.Remove(attachment);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("CanWriteJournals").WithName("DeleteAttachment");

        return app;
    }

    private static string? ValidateMagicBytes(byte[] header, string mimeType)
    {
        if (header.Length < 4) return null;

        if (mimeType is "image/jpeg" or "image/jpg")
        {
            if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
                return "image/jpeg";
        }

        if (mimeType is "image/png")
        {
            if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
                return "image/png";
        }

        if (mimeType is "image/heic" or "image/heif")
        {
            if (header.Length >= 12 && header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70)
            {
                if (header[8] == 0x68 || header[8] == 0x6D)
                    return "image/heic";
            }
        }

        if (mimeType is "application/pdf")
        {
            if (header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46)
                return "application/pdf";
        }

        if (mimeType is "video/mp4")
        {
            if (header.Length >= 8 && header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70)
                return "video/mp4";
        }

        return null;
    }
}
