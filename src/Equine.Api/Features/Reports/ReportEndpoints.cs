using Microsoft.AspNetCore.Mvc;

namespace Equine.Api.Features.Reports;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/app/reports").RequireAuthorization("CanManageBookings");

        group.MapGet("/behandlingar", GetTreatments).WithName("ReportTreatments");
        group.MapGet("/intakter", GetRevenue).WithName("ReportRevenue");
        group.MapGet("/uppfoljningar", GetFollowUps).WithName("ReportFollowUps");
        group.MapGet("/osignerade", GetUnsigned).WithName("ReportUnsigned");
        group.MapGet("/uteblivna", GetNoShows).WithName("ReportNoShows");
        group.MapGet("/klientaktivitet", GetDormant).WithName("ReportDormant");
        group.MapGet("/bokningskallor", GetSources).WithName("ReportSources");

        return app;
    }

    private static async Task<IResult> GetTreatments(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? format,
        ReportService reports,
        CancellationToken ct)
    {
        var range = DefaultRange(from, to);
        var rows = await reports.TreatmentsPerPeriodAsync(range.From, range.To, ct);
        if (IsCsv(format))
        {
            var csv = CsvExporter.ToCsv(
                ["Månad", "Behandling", "Antal", "Intäktsuppskattning"],
                rows.Select(r => new object?[] { r.Month, r.TreatmentName, r.Count, r.RevenueEstimate }));
            return CsvFile(csv, "behandlingar.csv");
        }
        return Results.Ok(rows);
    }

    private static async Task<IResult> GetRevenue(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? format,
        ReportService reports,
        CancellationToken ct)
    {
        var range = DefaultRange(from, to);
        var rows = await reports.RevenueOverviewAsync(range.From, range.To, ct);
        if (IsCsv(format))
        {
            var csv = CsvExporter.ToCsv(
                ["Månad", "Exkl moms", "Inkl moms", "Antal bokningar"],
                rows.Select(r => new object?[] { DateOnly.FromDateTime(r.Month), r.ExclVat, r.InclVat, r.BookingCount }));
            return CsvFile(csv, "intakter.csv");
        }
        return Results.Ok(rows);
    }

    private static async Task<IResult> GetFollowUps(
        [FromQuery] string? format,
        ReportService reports,
        CancellationToken ct)
    {
        var rows = await reports.FollowUpsAsync(ct);
        if (IsCsv(format))
        {
            var csv = CsvExporter.ToCsv(
                ["Häst", "Kund", "Senaste behandling", "Senast utförd", "Förföll", "Dagar sen"],
                rows.Select(r => new object?[]
                {
                    r.HorseName, r.OwnerName, r.LastTreatment, r.LastPerformedAt, r.DueAt, r.DaysOverdue
                }));
            return CsvFile(csv, "uppfoljningar.csv");
        }
        return Results.Ok(rows);
    }

    private static async Task<IResult> GetUnsigned(
        [FromQuery] string? format,
        ReportService reports,
        CancellationToken ct)
    {
        var rows = await reports.UnsignedJournalsAsync(ct);
        if (IsCsv(format))
        {
            var csv = CsvExporter.ToCsv(
                ["Journal-ID", "Häst", "Kund", "Behandling", "Skapad", "Timmar gammal"],
                rows.Select(r => new object?[]
                {
                    r.Id, r.HorseName, r.OwnerName, r.Treatment, r.CreatedAt, r.HoursOld
                }));
            return CsvFile(csv, "osignerade-journaler.csv");
        }
        return Results.Ok(rows);
    }

    private static async Task<IResult> GetNoShows(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? format,
        ReportService reports,
        CancellationToken ct)
    {
        var range = DefaultRange(from, to);
        var rows = await reports.NoShowsAsync(range.From, range.To, ct);
        if (IsCsv(format))
        {
            var csv = CsvExporter.ToCsv(
                ["Kund", "E-post", "Uteblivna", "Sen avbokning", "Totalt"],
                rows.Select(r => new object?[]
                {
                    r.OwnerName, r.Email, r.NoShows, r.LateCancellations, r.Total
                }));
            return CsvFile(csv, "uteblivna.csv");
        }
        return Results.Ok(rows);
    }

    private static async Task<IResult> GetDormant(
        [FromQuery] int months,
        [FromQuery] string? format,
        ReportService reports,
        CancellationToken ct)
    {
        months = months <= 0 ? 12 : Math.Clamp(months, 1, 60);
        var rows = await reports.DormantClientsAsync(months, ct);
        if (IsCsv(format))
        {
            var csv = CsvExporter.ToCsv(
                ["Kund", "E-post", "Telefon", "Senaste bokning"],
                rows.Select(r => new object?[]
                {
                    r.Name, r.Email, r.Phone, r.LastBookingAt
                }));
            return CsvFile(csv, "klientaktivitet.csv");
        }
        return Results.Ok(new { months, clients = rows });
    }

    private static async Task<IResult> GetSources(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? format,
        ReportService reports,
        CancellationToken ct)
    {
        var range = DefaultRange(from, to);
        var rows = await reports.BookingSourcesAsync(range.From, range.To, ct);
        if (IsCsv(format))
        {
            var csv = CsvExporter.ToCsv(
                ["Månad", "Källa", "Antal"],
                rows.Select(r => new object?[]
                {
                    DateOnly.FromDateTime(r.Month), r.Source, r.Count
                }));
            return CsvFile(csv, "bokningskallor.csv");
        }
        return Results.Ok(rows);
    }

    private static (DateOnly From, DateOnly To) DefaultRange(DateOnly? from, DateOnly? to)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = from ?? today.AddMonths(-3);
        var end = to ?? today;
        if (end < start) end = start;
        return (start, end);
    }

    private static bool IsCsv(string? format) =>
        string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase);

    private static IResult CsvFile(byte[] bytes, string filename) =>
        Results.File(bytes, "text/csv; charset=utf-8", filename);
}
