using Equine.Infrastructure.Export;

namespace Equine.Api.Features.Export;

public static class ExportEndpoints
{
    public static IEndpointRouteBuilder MapExportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/app/export").RequireAuthorization("CanAdminister");

        group.MapPost("/archive", async (IArchiveExportService export, CancellationToken ct) =>
        {
            var bytes = await export.ExportFullArchiveAsync(ct);
            return Results.File(bytes, "application/zip", $"hastjournal-archive-{DateTime.UtcNow:yyyyMMdd}.zip");
        }).WithName("ExportFullArchive");

        group.MapPost("/journals-pdf", async (IArchiveExportService export, CancellationToken ct) =>
        {
            var bytes = await export.ExportAllHorsePdfsAsync(ct);
            return Results.File(bytes, "application/zip", $"hastjournal-journals-{DateTime.UtcNow:yyyyMMdd}.zip");
        }).WithName("ExportAllHorsePdfs");

        return app;
    }
}
