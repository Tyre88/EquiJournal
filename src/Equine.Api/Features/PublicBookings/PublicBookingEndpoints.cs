using Equine.Api.Features.Bookings;
using Equine.Domain.Locations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Equine.Api.Features.PublicBookings;

public static class PublicBookingEndpoints
{
    public static IEndpointRouteBuilder MapPublicBookingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/public")
            .RequireCors("Public")
            .AllowAnonymous();

        group.MapGet("/treatments", async (PublicBookingService bookings, CancellationToken ct) =>
            Results.Ok(await bookings.ListTreatmentsAsync(ct)))
            .WithName("PublicTreatments")
            .RequireRateLimiting("public-read");

        group.MapGet("/slots", async (
            PublicBookingService bookings,
            [FromQuery] Guid treatmentId,
            [FromQuery] DateOnly from,
            [FromQuery] DateOnly to,
            [FromQuery] string postcode,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(postcode) || !AddressNormalization.IsValidSwedishPostcode(postcode))
                return Results.BadRequest(new { message = "Ogiltigt postnummer." });
            if (treatmentId == Guid.Empty)
                return Results.BadRequest(new { message = "Behandling saknas." });
            var starts = await bookings.GetSlotStartsAsync(treatmentId, from, to, postcode, ct);
            return Results.Ok(new { starts });
        }).WithName("PublicSlots").RequireRateLimiting("public-read");

        group.MapPost("/bookings", async (
            PublicCreateBookingRequest request,
            PublicBookingService bookings,
            PublicBookingRateGuard rate,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!rate.TryAcceptEmail(request.Owner.Email))
                return Results.StatusCode(StatusCodes.Status429TooManyRequests);
            try
            {
                var reference = await bookings.CreateAsync(request, ClientIp(http), ct);
                return Results.Created("/api/public/bookings", new { reference });
            }
            catch (BookingConflictException ex)
            {
                return Results.Conflict(new
                {
                    message = "Tiden är inte längre ledig.",
                    slots = ex.Slots.Select(s => s.StartsAt)
                });
            }
        }).WithName("PublicCreateBooking").RequireRateLimiting("public-book");

        group.MapPost("/bookings/verify", async (
            PublicVerifyRequest request,
            PublicBookingService bookings,
            HttpContext http,
            CancellationToken ct) =>
        {
            try
            {
                await bookings.VerifyAsync(request.Token, ClientIp(http), ct);
                return Results.Ok();
            }
            catch (KeyNotFoundException)
            {
                return TokenMissing();
            }
        }).WithName("PublicVerifyBooking").RequireRateLimiting("public-read");

        group.MapGet("/bookings/{token}", async (string token, PublicBookingService bookings, CancellationToken ct) =>
        {
            var summary = await bookings.GetSummaryAsync(token, ct);
            return summary is null ? TokenMissing() : Results.Ok(summary);
        }).WithName("PublicGetBooking").RequireRateLimiting("public-read");

        group.MapPost("/bookings/{token}/cancel", async (
            string token,
            PublicCancelRequest? request,
            PublicBookingService bookings,
            CancellationToken ct) =>
        {
            try
            {
                await bookings.CancelAsync(token, request?.Reason, ct);
                return Results.Ok();
            }
            catch (KeyNotFoundException)
            {
                return TokenMissing();
            }
        }).WithName("PublicCancelBooking").RequireRateLimiting("public-read");

        group.MapPost("/bookings/{token}/reschedule", async (
            string token,
            PublicRescheduleRequest request,
            PublicBookingService bookings,
            CancellationToken ct) =>
        {
            try
            {
                await bookings.RescheduleAsync(token, request.StartsAt, ct);
                return Results.Ok();
            }
            catch (KeyNotFoundException)
            {
                return TokenMissing();
            }
            catch (BookingConflictException ex)
            {
                return Results.Conflict(new
                {
                    message = "Tiden är inte längre ledig.",
                    slots = ex.Slots.Select(s => s.StartsAt)
                });
            }
        }).WithName("PublicRescheduleBooking").RequireRateLimiting("public-read");

        return app;
    }

    private static IResult TokenMissing() =>
        Results.NotFound(PublicBookingService.TokenNotFound);

    private static string? ClientIp(HttpContext http) =>
        http.Connection.RemoteIpAddress?.ToString();
}
