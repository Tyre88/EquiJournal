using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Equine.Api.Features.Reports;

internal static class DriveLogPdf
{
    private static readonly CultureInfo Sv = CultureInfo.GetCultureInfo("sv-SE");

    public static byte[] Render(DriveLogReport report)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var meta = report.Meta;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(8).FontFamily("Arial"));

                page.Header().Column(col =>
                {
                    col.Item().Text("Körjournal").FontSize(16).SemiBold();
                    col.Item().Text($"{meta.Clinic}  ·  Förare: {meta.DriverName}");
                    col.Item().Text(
                        $"Registreringsnummer: {(string.IsNullOrWhiteSpace(meta.VehicleRegistrationNumber) ? "—" : meta.VehicleRegistrationNumber)}"
                        + $"  ·  Period: {meta.From.ToString("yyyy-MM-dd", Sv)} – {meta.To.ToString("yyyy-MM-dd", Sv)}");
                    col.Item().Text($"Antal resor: {meta.TripCount.ToString(Sv)}  ·  Total körsträcka: {meta.TotalKm.ToString("0.#", Sv)} km");
                    col.Item().PaddingTop(4).Text(meta.Disclaimer).Italic().FontSize(7);
                    col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                });

                page.Content().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(62);
                        columns.ConstantColumn(40);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.ConstantColumn(40);
                        columns.ConstantColumn(70);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1.4f);
                        columns.RelativeColumn(1.4f);
                    });

                    table.Header(header =>
                    {
                        foreach (var title in new[]
                        {
                            "Datum", "Tid", "Startadress", "Slutadress", "Km", "Ärende",
                            "Besökta platser", "Kontakt", "Hästar / behandlingar"
                        })
                        {
                            header.Cell().Element(HeaderCell).Text(title);
                        }
                    });

                    foreach (var trip in report.Trips)
                    {
                        table.Cell().Element(BodyCell).Text(trip.Date.ToString("yyyy-MM-dd", Sv));
                        table.Cell().Element(BodyCell).Text(trip.StartsAt.ToOffset(StockholmOffset(trip.StartsAt)).ToString("HH:mm", Sv));
                        table.Cell().Element(BodyCell).Text(trip.FromAddress);
                        table.Cell().Element(BodyCell).Text(trip.ToAddress);
                        table.Cell().Element(BodyCell).Text(trip.DistanceKm?.ToString("0.#", Sv) ?? "—");
                        table.Cell().Element(BodyCell).Text(trip.Purpose);
                        table.Cell().Element(BodyCell).Text(Visited(trip));
                        table.Cell().Element(BodyCell).Text(trip.Contacts);
                        table.Cell().Element(BodyCell).Text(HorsesAndTreatments(trip));
                    }
                });

                page.Footer().AlignRight().DefaultTextStyle(x => x.FontSize(8)).Text(t =>
                {
                    t.Span("Sida ");
                    t.CurrentPageNumber();
                    t.Span(" av ");
                    t.TotalPages();
                    t.Span($"  ·  Utskriven {DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(2)):yyyy-MM-dd HH:mm} Europe/Stockholm");
                });
            });
        }).GeneratePdf();
    }

    private static string Visited(DriveLogTripRow trip) =>
        string.IsNullOrWhiteSpace(trip.PlaceVisited) ? "" : $"{trip.PlaceVisited}, {trip.ToAddress}";

    private static string HorsesAndTreatments(DriveLogTripRow trip)
    {
        if (string.IsNullOrWhiteSpace(trip.Horses) && string.IsNullOrWhiteSpace(trip.Treatments))
            return "";
        if (string.IsNullOrWhiteSpace(trip.Treatments)) return trip.Horses;
        if (string.IsNullOrWhiteSpace(trip.Horses)) return trip.Treatments;
        return $"{trip.Horses} ({trip.Treatments})";
    }

    private static TimeSpan StockholmOffset(DateTimeOffset value)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");
        return zone.GetUtcOffset(value);
    }

    private static IContainer HeaderCell(IContainer container) =>
        container.DefaultTextStyle(x => x.SemiBold().FontSize(7))
            .BorderBottom(1).BorderColor(Colors.Grey.Darken1)
            .PaddingVertical(3).PaddingRight(4);

    private static IContainer BodyCell(IContainer container) =>
        container.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(3).PaddingRight(4);
}
