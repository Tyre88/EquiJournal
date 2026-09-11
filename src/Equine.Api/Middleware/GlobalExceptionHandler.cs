using Equine.Api.Features.Bookings;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Equine.Api.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var correlationId = httpContext.Request.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? Guid.CreateVersion7().ToString();

        _logger.LogError(exception, "Request {CorrelationId} failed: {Message}", correlationId, exception.Message);

        var isPublic = httpContext.Request.Path.StartsWithSegments("/api/public");

        if (exception is BookingConflictException conflict)
        {
            httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
            httpContext.Response.ContentType = "application/json";
            object payload = isPublic
                ? new { message = "Tiden är inte längre ledig.", slots = conflict.Slots.Select(s => s.StartsAt) }
                : new { message = conflict.Message, slots = conflict.Slots };
            await httpContext.Response.WriteAsync(JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }), cancellationToken);
            return true;
        }

        var hideDetails = isPublic
            || httpContext.Request.Host.Host.Contains("prod", StringComparison.OrdinalIgnoreCase)
            || httpContext.Request.Host.Host.Contains("staging", StringComparison.OrdinalIgnoreCase);

        var problemDetails = new ProblemDetails
        {
            Status = exception switch
            {
                ArgumentException => StatusCodes.Status400BadRequest,
                KeyNotFoundException => StatusCodes.Status404NotFound,
                InvalidOperationException => StatusCodes.Status409Conflict,
                UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
                _ => StatusCodes.Status500InternalServerError
            },
            Detail = exception is ArgumentException && isPublic
                ? exception.Message
                : hideDetails ? "Ett fel uppstod." : exception.ToString(),
            Instance = httpContext.Request.Path,
        };

        if (!hideDetails && !isPublic) problemDetails.Extensions["stackTrace"] = exception.StackTrace;

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        httpContext.Response.StatusCode = (int)problemDetails.Status!;
        httpContext.Response.ContentType = "application/problem+json";

        var body = JsonSerializer.Serialize(problemDetails, jsonOptions);
        await httpContext.Response.WriteAsync(body, cancellationToken);
        return true;
    }
}
