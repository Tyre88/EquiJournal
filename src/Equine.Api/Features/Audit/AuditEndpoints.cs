using Equine.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Equine.Api.Features.Audit;

public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/app/audit").RequireAuthorization("CanManageBookings");

        group.MapGet("/", ListAudit).WithName("ListAudit");
        group.MapGet("/export", ExportAudit).WithName("ExportAudit");
        group.MapGet("/entity/{entityType}/{entityId}", EntityHistory).WithName("EntityAuditHistory");

        return app;
    }

    private static async Task<IResult> ListAudit(
        [FromQuery] string? actor,
        [FromQuery] string? entityType,
        [FromQuery] string? entityId,
        [FromQuery] string? action,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        EquineDbContext db,
        CancellationToken ct)
    {
        var p = page is null or <= 0 ? 1 : page.Value;
        var ps = pageSize is null or <= 0 ? 50 : Math.Clamp(pageSize.Value, 1, 200);

        var query = db.AuditLog.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(actor))
            query = query.Where(a => a.ActorId == actor);
        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(a => a.EntityType == entityType);
        if (!string.IsNullOrWhiteSpace(entityId))
            query = query.Where(a => a.EntityId == entityId);
        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(a => a.Action == action);
        if (from is not null)
            query = query.Where(a => a.Timestamp >= from);
        if (to is not null)
            query = query.Where(a => a.Timestamp <= to);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((p - 1) * ps)
            .Take(ps)
            .Select(a => new
            {
                a.Id,
                a.ActorId,
                a.Action,
                a.EntityType,
                a.EntityId,
                a.Before,
                a.After,
                a.Timestamp
            })
            .ToListAsync(ct);

        return Results.Ok(new { total, page = p, pageSize = ps, items });
    }

    private static async Task<IResult> ExportAudit(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        EquineDbContext db,
        CancellationToken ct)
    {
        var query = db.AuditLog.AsNoTracking().AsQueryable();
        if (from is not null)
            query = query.Where(a => a.Timestamp >= from);
        if (to is not null)
            query = query.Where(a => a.Timestamp <= to);

        var rows = await query
            .OrderByDescending(a => a.Timestamp)
            .Take(100_000)
            .Select(a => new { a.Timestamp, a.ActorId, a.Action, a.EntityType, a.EntityId })
            .ToListAsync(ct);

        var csv = Reports.CsvExporter.ToCsv(
            ["Tidpunkt", "Användare", "Åtgärd", "Entitetstyp", "Entitets-ID"],
            rows.Select(r => new object?[] { r.Timestamp, r.ActorId, r.Action, r.EntityType, r.EntityId }));

        return Results.File(csv, "text/csv; charset=utf-8", "audit-log.csv");
    }

    private static async Task<IResult> EntityHistory(
        string entityType,
        string entityId,
        EquineDbContext db,
        CancellationToken ct)
    {
        var items = await db.AuditLog
            .AsNoTracking()
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.Timestamp)
            .Take(100)
            .Select(a => new
            {
                a.Id,
                a.ActorId,
                a.Action,
                a.Before,
                a.After,
                a.Timestamp
            })
            .ToListAsync(ct);

        return Results.Ok(items);
    }
}
