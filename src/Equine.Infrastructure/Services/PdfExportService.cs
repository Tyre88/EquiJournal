using System.Globalization;
using System.Text.Json;
using Equine.Domain.Entities;
using Equine.Infrastructure.Practice;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Equine.Infrastructure.Services;

public interface IPdfExportService
{
    Task<byte[]> ExportJournalEntryAsync(JournalEntry journal, CancellationToken cancellationToken = default);
    Task<byte[]> ExportHorseJournalHistoryAsync(Horse horse, CancellationToken cancellationToken = default);
}

public class PdfExportService : IPdfExportService
{
    private readonly PracticeSettingsService _practice;
    private static readonly CultureInfo Sv = CultureInfo.GetCultureInfo("sv-SE");

    public PdfExportService(PracticeSettingsService practice)
    {
        _practice = practice;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<byte[]> ExportJournalEntryAsync(JournalEntry journal, CancellationToken cancellationToken = default)
    {
        var practitioner = await _practice.GetAsync(cancellationToken);
        return Document.Create(container =>
        {
            container.Page(page => ConfigurePage(page, practitioner, journal.Horse?.Name, () =>
            {
                page.Content().Element(c => ComposeJournal(c, journal));
            }));
        }).GeneratePdf();
    }

    public async Task<byte[]> ExportHorseJournalHistoryAsync(Horse horse, CancellationToken cancellationToken = default)
    {
        var practitioner = await _practice.GetAsync(cancellationToken);
        return Document.Create(container =>
        {
            container.Page(page => ConfigurePage(page, practitioner, horse.Name, () =>
            {
                page.Content().Column(col =>
                {
                    col.Spacing(10);
                    col.Item().Text($"Journalhistorik — {horse.Name}").FontSize(16).SemiBold();
                    col.Item().Text($"Ägare: {horse.Owner?.Name ?? "—"}  ·  Art: {horse.Species}  ·  Kön: {horse.Sex}");

                    if (horse.JournalEntries.Count == 0)
                    {
                        col.Item().Text("Inga journalanteckningar finns för denna häst.");
                        return;
                    }

                    foreach (var journal in horse.JournalEntries.OrderByDescending(j => j.PerformedAt))
                    {
                        col.Item().PaddingTop(8).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
                        col.Item().Element(c => ComposeJournal(c, journal, compact: true));
                    }
                });
            }));
        }).GeneratePdf();
    }

    private static void ConfigurePage(PageDescriptor page, PracticeSettings practitioner, string? horseName, Action compose)
    {
        page.Margin(40);
        page.Size(PageSizes.A4);
        page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

        page.Header().Element(header =>
        {
            header.Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(left =>
                    {
                        left.Item().Text(practitioner.Clinic).FontSize(14).SemiBold();
                        if (!string.IsNullOrWhiteSpace(practitioner.Name))
                            left.Item().Text(practitioner.Name);
                        if (!string.IsNullOrWhiteSpace(practitioner.Address))
                            left.Item().Text(practitioner.Address);
                        if (!string.IsNullOrWhiteSpace(practitioner.Phone) || !string.IsNullOrWhiteSpace(practitioner.Email))
                            left.Item().Text($"{practitioner.Phone}  {practitioner.Email}".Trim());
                    });
                    row.RelativeItem().AlignRight().Text(horseName ?? "Journal").FontSize(12);
                });
                col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Darken1);
            });
        });

        compose();

        page.Footer().Element(footer =>
        {
            footer.Column(col =>
            {
                col.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                col.Item().PaddingTop(6).Row(row =>
                {
                    row.RelativeItem().Column(sig =>
                    {
                        sig.Item().Text("Signatur").SemiBold();
                        sig.Item().Text($"Behandlare: {practitioner.Name}");
                        sig.Item().Text($"Klinik: {practitioner.Clinic}");
                    });
                    row.RelativeItem().AlignRight().DefaultTextStyle(x => x.FontSize(9)).Text(t =>
                    {
                        t.Span("Sida ");
                        t.CurrentPageNumber();
                        t.Span(" av ");
                        t.TotalPages();
                        t.Span($"  ·  Utskriven {DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(2)):yyyy-MM-dd HH:mm} Europe/Stockholm");
                    });
                });
            });
        });
    }

    private static void ComposeJournal(IContainer container, JournalEntry j, bool compact = false)
    {
        container.Column(col =>
        {
            col.Spacing(6);
            var title = compact
                ? $"Journal {j.PerformedAt.ToOffset(TimeSpan.FromHours(2)):yyyy-MM-dd HH:mm}"
                : "Journalanteckning";
            col.Item().Text(title).FontSize(compact ? 12 : 16).SemiBold();

            col.Item().Element(c => Section(c, "Patient", new[]
            {
                $"Häst: {j.Horse?.Name ?? "—"}",
                $"Ägare: {OwnerLabel(j)}",
                $"Art: {j.Species}",
                $"Kön: {j.Sex ?? "Okänt"}",
                $"Ålder: {j.AgeOrAgeGroup ?? "—"}",
                $"Identitet: {j.AnimalIdentity ?? "—"}"
            }));

            col.Item().Element(c => Section(c, "Behandling", new[]
            {
                $"Typ: {j.TreatmentTypeName ?? "—"}",
                $"Utförd: {j.PerformedAt.ToOffset(TimeSpan.FromHours(2)):yyyy-MM-dd HH:mm}",
                $"Duration: {j.DurationMinutes?.ToString(Sv) ?? "—"} min",
                $"Pris: {j.Price?.ToString("N2", Sv) ?? "—"} SEK"
            }));

            col.Item().Element(c => Section(c, "Anamnes", new[] { j.Anamnes }));
            col.Item().Element(c => Section(c, "Status / kliniska fynd", new[] { j.StatusKlinisk }));
            col.Item().Element(c => Section(c, "Åtgärder", new[] { j.Atgarder }));
            if (!string.IsNullOrWhiteSpace(j.Diagnos))
                col.Item().Element(c => Section(c, "Diagnos", new[] { j.Diagnos }));
            if (!string.IsNullOrWhiteSpace(j.Differentialdiagnoser))
                col.Item().Element(c => Section(c, "Differentialdiagnoser", new[] { j.Differentialdiagnoser }));
            if (!string.IsNullOrWhiteSpace(j.PrognosOchPlan))
                col.Item().Element(c => Section(c, "Prognos och hemgångsråd", new[] { j.PrognosOchPlan }));

            foreach (var line in TemplateLines(j.TemplateDataJson))
                col.Item().Text(line);

            if (j.Status == JournalStatus.Signed)
            {
                col.Item().Element(c => Section(c, "Signering", new[]
                {
                    $"Status: Signerad",
                    $"Signerad: {j.SignedAt?.ToOffset(TimeSpan.FromHours(2)):yyyy-MM-dd HH:mm} av {j.SignedBy}",
                    $"Innehållshash: {j.ContentHash ?? "—"}"
                }));
            }
            else
            {
                col.Item().Text("Status: Utkast").Italic();
            }

            if (j.Amendments.Count > 0)
            {
                col.Item().Text("Tillägg").SemiBold();
                foreach (var a in j.Amendments.OrderBy(x => x.CreatedAt))
                {
                    col.Item().Text($"{a.CreatedAt.ToOffset(TimeSpan.FromHours(2)):yyyy-MM-dd HH:mm} · {a.CreatedBy}");
                    col.Item().Text($"Anledning: {a.Reason}");
                    col.Item().Text(a.Text);
                }
            }

            if (j.Attachments.Count > 0)
            {
                col.Item().Text("Bilagor").SemiBold();
                foreach (var att in j.Attachments.OrderBy(x => x.CreatedAt))
                    col.Item().Text($"• {att.OriginalFileName} ({att.ContentType}, {att.Size} byte)");
            }
        });
    }

    private static void Section(IContainer container, string title, IEnumerable<string> lines)
    {
        container.Column(col =>
        {
            col.Item().Text(title).SemiBold();
            foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
                col.Item().Text(line);
        });
    }

    private static string OwnerLabel(JournalEntry j)
    {
        if (string.IsNullOrWhiteSpace(j.OwnerSnapshot))
            return j.Horse?.Owner?.Name ?? "—";
        try
        {
            using var doc = JsonDocument.Parse(j.OwnerSnapshot);
            var root = doc.RootElement;
            var name = root.TryGetProperty("Name", out var n) ? n.GetString() : null;
            name ??= root.TryGetProperty("name", out var n2) ? n2.GetString() : null;
            return name ?? j.Horse?.Owner?.Name ?? "—";
        }
        catch
        {
            return j.Horse?.Owner?.Name ?? "—";
        }
    }

    private static IEnumerable<string> TemplateLines(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
            yield break;

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch { yield break; }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                yield break;

            yield return "Mallfält";
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.NameEquals("markers") || prop.Value.ValueKind == JsonValueKind.Object && prop.Value.TryGetProperty("markers", out _))
                {
                    yield return $"{prop.Name}: kroppskarta";
                    continue;
                }

                if (TryAnatomyLines(prop.Name, prop.Value, out var anatomyLines))
                {
                    foreach (var line in anatomyLines)
                        yield return line;
                    continue;
                }

                yield return $"{prop.Name}: {prop.Value}";
            }
        }
    }

    private static bool TryAnatomyLines(string fieldName, JsonElement value, out List<string> lines)
    {
        lines = [];
        JsonElement root = value;
        JsonDocument? owned = null;
        try
        {
            if (value.ValueKind == JsonValueKind.String)
            {
                var raw = value.GetString();
                if (string.IsNullOrWhiteSpace(raw) || raw[0] != '{')
                    return false;
                owned = JsonDocument.Parse(raw);
                root = owned.RootElement;
            }

            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("annotations", out var anns) || anns.ValueKind != JsonValueKind.Array)
                return false;

            lines.Add($"{fieldName}:");
            if (anns.GetArrayLength() == 0)
            {
                lines.Add("Inga markerade områden");
            }
            else
            {
                foreach (var ann in anns.EnumerateArray())
                {
                    if (ann.ValueKind != JsonValueKind.Object) continue;
                    var label = ann.TryGetProperty("label", out var l) && l.ValueKind == JsonValueKind.String ? l.GetString() : null;
                    var regionId = ann.TryGetProperty("regionId", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null;
                    var side = ann.TryGetProperty("side", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
                    var finding = ann.TryGetProperty("finding", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString() : null;
                    var note = ann.TryGetProperty("note", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
                    var sideLabel = side is "L" or "l" ? "V" : side is "R" or "r" ? "H" : side;
                    var name = string.IsNullOrWhiteSpace(label) ? regionId ?? "område" : label;
                    var line = string.IsNullOrWhiteSpace(sideLabel)
                        ? $"{name}: {finding ?? "—"}"
                        : $"{name} ({sideLabel}): {finding ?? "—"}";
                    if (!string.IsNullOrWhiteSpace(note))
                        line += $" — {note}";
                    lines.Add(line);
                }
            }

            if (root.TryGetProperty("strokes", out var strokes)
                && strokes.ValueKind == JsonValueKind.Array
                && strokes.GetArrayLength() > 0)
            {
                lines.Add($"Friteckning: {strokes.GetArrayLength()} streck");
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        finally
        {
            owned?.Dispose();
        }
    }
}
