using Equine.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc;

namespace Equine.Api.Features.Settings;

public static class AnatomyImageEndpoints
{
    private static readonly TimeSpan SignedUrlLifetime = TimeSpan.FromMinutes(15);
    private const long MaxFileSize = 5L * 1024 * 1024;
    public const string KeyPrefix = "practice/anatomy/";

    public static IEndpointRouteBuilder MapAnatomyImageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/app/settings/anatomy-images").RequireAuthorization("CanAdminister");

        group.MapPost("/", async (IFormFile file, IObjectStorage storage) =>
        {
            if (file is null || file.Length == 0)
                return Results.BadRequest("No file provided.");

            if (file.Length > MaxFileSize)
                return Results.BadRequest("File too large (max 5MB).");

            await using var stream = file.OpenReadStream();
            var headerBytes = new byte[8];
            await stream.ReadExactlyAsync(headerBytes.AsMemory(0, (int)Math.Min(8, file.Length)));
            stream.Position = 0;

            var contentType = ValidateImageMagicBytes(headerBytes, file.ContentType);
            if (contentType is null)
                return Results.BadRequest("Only JPEG and PNG images are allowed.");

            var ext = contentType == "image/png" ? "png" : "jpg";
            var storageKey = $"{KeyPrefix}{Guid.CreateVersion7()}.{ext}";
            await storage.PutAsync(storageKey, stream, contentType);

            var expiresAt = DateTimeOffset.UtcNow.Add(SignedUrlLifetime);
            return Results.Ok(new
            {
                Key = storageKey,
                Url = storage.GetPresignedUrl(storageKey, SignedUrlLifetime),
                ExpiresAt = expiresAt,
                ContentType = contentType
            });
        }).DisableAntiforgery().WithName("UploadAnatomyImage");

        group.MapGet("/url", async (string key, IObjectStorage storage) =>
        {
            if (!IsSafeKey(key))
                return Results.BadRequest("Invalid anatomy image key.");

            if (!await storage.ExistsAsync(key))
                return Results.NotFound();

            var expiresAt = DateTimeOffset.UtcNow.Add(SignedUrlLifetime);
            return Results.Ok(new
            {
                Key = key,
                Url = storage.GetPresignedUrl(key, SignedUrlLifetime),
                ExpiresAt = expiresAt
            });
        }).WithName("GetAnatomyImageUrl");

        return app;
    }

    internal static bool IsSafeKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        if (!key.StartsWith(KeyPrefix, StringComparison.Ordinal)) return false;
        if (key.Contains("..", StringComparison.Ordinal) || key.Contains('\\', StringComparison.Ordinal))
            return false;
        return key.Length < 200;
    }

    private static string? ValidateImageMagicBytes(byte[] header, string mimeType)
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

        return null;
    }
}
