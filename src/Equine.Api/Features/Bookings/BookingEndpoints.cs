using Equine.Api.Auth;
using Equine.Domain.Entities;
using Equine.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Equine.Api.Features.Bookings;

public static class BookingEndpoints
{
    public static IEndpointRouteBuilder MapBookingEndpoints(this IEndpointRouteBuilder app)
    {
        var bookings = app.MapGroup("/api/app/bookings").RequireAuthorization("CanManageBookings");
        var visits = app.MapGroup("/api/app/visits").RequireAuthorization("CanManageBookings");

        bookings.MapGet("/", async (
            EquineDbContext db,
            [FromQuery] DateTimeOffset? from = null,
            [FromQuery] DateTimeOffset? to = null,
            [FromQuery] BookingStatus? status = null,
            [FromQuery] Guid? practitionerId = null,
            [FromQuery] Guid? horseId = null,
            [FromQuery] Guid? ownerId = null,
            [FromQuery] BookingSource? source = null,
            [FromQuery] bool? emailVerified = null) =>
        {
            var query = db.BookingLines
                .Include(l => l.Visit).ThenInclude(v => v.Location)
                .Include(l => l.Horse)
                .Include(l => l.Owner)
                .Include(l => l.TreatmentType)
                .AsQueryable();

            if (from is not null) query = query.Where(l => l.Visit.EndsAt > from);
            if (to is not null) query = query.Where(l => l.Visit.StartsAt < to);
            if (status is not null) query = query.Where(l => l.Status == status);
            if (practitionerId is not null) query = query.Where(l => l.Visit.PractitionerId == practitionerId);
            if (horseId is not null) query = query.Where(l => l.HorseId == horseId);
            if (ownerId is not null) query = query.Where(l => l.OwnerId == ownerId);
            if (source is not null) query = query.Where(l => l.Source == source);
            if (emailVerified == true) query = query.Where(l => l.EmailVerifiedAt != null);
            if (emailVerified == false) query = query.Where(l => l.EmailVerifiedAt == null);

            var lines = await query.ToListAsync();
            var items = lines
                .OrderBy(l => l.Visit.DeriveLineStart(l))
                .Select(BookingDtos.ToListItem)
                .ToList();
            return Results.Ok(items);
        }).WithName("ListBookings");

        bookings.MapGet("/{id:guid}", async (Guid id, EquineDbContext db) =>
        {
            var line = await db.BookingLines
                .Include(l => l.Visit).ThenInclude(v => v.Location)
                .Include(l => l.Visit).ThenInclude(v => v.Lines)
                .Include(l => l.Horse)
                .Include(l => l.Owner)
                .Include(l => l.TreatmentType)
                .FirstOrDefaultAsync(l => l.Id == id);
            if (line is null) return Results.NotFound();
            return Results.Ok(BookingDtos.ToListItem(line));
        }).WithName("GetBooking");

        bookings.MapGet("/{id:guid}/notifications", async (Guid id, EquineDbContext db) =>
        {
            var logs = await db.NotificationLog
                .Where(l => l.RelatedEntityId == id)
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new
                {
                    l.Id,
                    type = l.Type.ToString(),
                    channel = l.Channel.ToString(),
                    l.Recipient,
                    l.Subject,
                    l.Status,
                    l.CreatedAt
                })
                .ToListAsync();
            return Results.Ok(logs);
        }).WithName("GetBookingNotifications");

        bookings.MapPost("/", async (
            CreateBookingRequest request,
            HttpContext http,
            BookingCommandService commands) =>
        {
            var userId = CurrentUser.GetUserId(http);
            if (userId is null) return Results.Unauthorized();

            try
            {
                var visit = await commands.CreateManualAsync(
                    request.PractitionerId ?? userId.Value,
                    userId.Value,
                    request.StartsAt,
                    request.EndsAt,
                    request.LocationId,
                    request.Source ?? BookingSource.Manual,
                    request.Lines.Select(l => new BookingLineRequest(l.HorseId, l.TreatmentTypeId, l.ClientNote, l.InternalNote)).ToList());
                return Results.Created($"/api/app/visits/{visit.Id}", new { visit.Id, lineIds = visit.Lines.Select(l => l.Id) });
            }
            catch (BookingConflictException ex)
            {
                return Results.Conflict(new { message = ex.Message, slots = ex.Slots });
            }
        }).WithName("CreateBooking");

        bookings.MapPatch("/{id:guid}", async (
            Guid id,
            PatchBookingRequest request,
            EquineDbContext db) =>
        {
            var line = await db.BookingLines.FirstOrDefaultAsync(l => l.Id == id);
            if (line is null) return Results.NotFound();
            line.UpdateNotes(request.ClientNote, request.InternalNote, request.ClientNote is not null, request.InternalNote is not null);
            await db.SaveChangesAsync();
            return Results.Ok();
        }).WithName("PatchBooking");

        bookings.MapPost("/{id:guid}/approve", async (Guid id, HttpContext http, BookingCommandService commands) =>
        {
            var userId = CurrentUser.GetUserId(http);
            if (userId is null) return Results.Unauthorized();
            await commands.ApproveAsync(id, userId.Value);
            return Results.Ok();
        }).WithName("ApproveBooking");

        bookings.MapPost("/{id:guid}/cancel", async (Guid id, CancelBookingRequest? request, HttpContext http, BookingCommandService commands) =>
        {
            var userId = CurrentUser.GetUserId(http);
            if (userId is null) return Results.Unauthorized();
            await commands.CancelAsync(id, userId.Value, request?.Reason);
            return Results.Ok();
        }).WithName("CancelBooking");

        bookings.MapPost("/{id:guid}/no-show", async (Guid id, HttpContext http, BookingCommandService commands) =>
        {
            var userId = CurrentUser.GetUserId(http);
            if (userId is null) return Results.Unauthorized();
            await commands.MarkNoShowAsync(id, userId.Value);
            return Results.Ok();
        }).WithName("NoShowBooking");

        bookings.MapPost("/{id:guid}/complete", async (Guid id, HttpContext http, BookingCommandService commands) =>
        {
            var userId = CurrentUser.GetUserId(http);
            if (userId is null) return Results.Unauthorized();
            var journalId = await commands.CompleteAsync(id, userId.Value);
            return Results.Ok(new { bookingId = id, journalId });
        }).RequireAuthorization("CanWriteJournals").WithName("CompleteBooking");

        visits.MapGet("/{id:guid}", async (Guid id, EquineDbContext db) =>
        {
            var visit = await db.Visits
                .Include(v => v.Location)
                .Include(v => v.Lines).ThenInclude(l => l.Horse)
                .Include(v => v.Lines).ThenInclude(l => l.Owner)
                .Include(v => v.Lines).ThenInclude(l => l.TreatmentType)
                .FirstOrDefaultAsync(v => v.Id == id);
            if (visit is null) return Results.NotFound();
            return Results.Ok(BookingDtos.ToVisitDto(visit));
        }).WithName("GetVisit");

        visits.MapPatch("/{id:guid}", async (
            Guid id,
            PatchVisitRequest request,
            HttpContext http,
            BookingCommandService commands) =>
        {
            var userId = CurrentUser.GetUserId(http);
            if (userId is null) return Results.Unauthorized();
            try
            {
                if (request.StartsAt is DateTimeOffset starts)
                    await commands.RescheduleAsync(id, userId.Value, starts);
                if (request.LocationId is Guid locationId)
                    await commands.ChangeLocationAsync(id, locationId, userId.Value);
                return Results.Ok();
            }
            catch (BookingConflictException ex)
            {
                return Results.Conflict(new { message = ex.Message, slots = ex.Slots });
            }
        }).WithName("PatchVisit");

        visits.MapPost("/{id:guid}/lines", async (
            Guid id,
            AddLineRequest request,
            HttpContext http,
            BookingCommandService commands) =>
        {
            var userId = CurrentUser.GetUserId(http);
            if (userId is null) return Results.Unauthorized();
            try
            {
                var line = await commands.AddLineAsync(id, userId.Value,
                    new BookingLineRequest(request.HorseId, request.TreatmentTypeId, request.ClientNote, request.InternalNote));
                return Results.Created($"/api/app/bookings/{line.Id}", new { line.Id });
            }
            catch (BookingConflictException ex)
            {
                return Results.Conflict(new { message = ex.Message, slots = ex.Slots });
            }
        }).WithName("AddVisitLine");

        return app;
    }

    public record CreateBookingLineDto(Guid HorseId, Guid TreatmentTypeId, string? ClientNote = null, string? InternalNote = null);

    public record CreateBookingRequest(
        DateTimeOffset StartsAt,
        IReadOnlyList<CreateBookingLineDto> Lines,
        DateTimeOffset? EndsAt = null,
        Guid? LocationId = null,
        Guid? PractitionerId = null,
        BookingSource? Source = null);

    public record PatchBookingRequest(string? ClientNote = null, string? InternalNote = null);
    public record CancelBookingRequest(string? Reason = null);
    public record PatchVisitRequest(DateTimeOffset? StartsAt = null, Guid? LocationId = null);
    public record AddLineRequest(Guid HorseId, Guid TreatmentTypeId, string? ClientNote = null, string? InternalNote = null);
}
