using System.Globalization;
using System.Text.Json;
using Equine.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Equine.Infrastructure.Import;

public sealed record ImportSkip(string Entity, int Row, string Reason, string? Key = null);

public sealed class HistoricImportReport
{
    public bool DryRun { get; init; } = true;
    public int OwnersIn { get; set; }
    public int HorsesIn { get; set; }
    public int JournalsIn { get; set; }
    public int OwnersCreated { get; set; }
    public int HorsesCreated { get; set; }
    public int JournalsCreated { get; set; }
    public List<ImportSkip> Skipped { get; } = [];
    public List<string> Duplicates { get; } = [];
}

public sealed class HistoricImportService
{
    private readonly EquineDbContext _db;

    public HistoricImportService(EquineDbContext db) => _db = db;

    public async Task<HistoricImportReport> ImportAsync(
        IReadOnlyList<IReadOnlyDictionary<string, string>> owners,
        IReadOnlyList<IReadOnlyDictionary<string, string>> horses,
        IReadOnlyList<IReadOnlyDictionary<string, string>> journals,
        bool commit,
        string? signedByOverride = null,
        Guid? createdBy = null,
        CancellationToken cancellationToken = default)
    {
        var report = new HistoricImportReport { DryRun = !commit };
        report.OwnersIn = owners.Count;
        report.HorsesIn = horses.Count;
        report.JournalsIn = journals.Count;

        var signedBy = signedByOverride;
        if (string.IsNullOrWhiteSpace(signedBy))
        {
            var name = await _db.PracticeSettings.Select(p => p.Name).FirstOrDefaultAsync(cancellationToken);
            signedBy = $"{(string.IsNullOrWhiteSpace(name) ? "Import" : name)} (import)";
        }
        var actor = createdBy ?? Guid.CreateVersion7();

        var ownerByEmail = await _db.Owners.ToDictionaryAsync(o => o.Email.ToLowerInvariant(), cancellationToken);
        var ownerByPhone = _db.Owners.AsEnumerable()
            .Where(o => !string.IsNullOrWhiteSpace(o.Phone))
            .GroupBy(o => NormalizePhone(o.Phone!))
            .Where(g => g.Key.Length > 0)
            .ToDictionary(g => g.Key, g => g.First());
        var horseByOwnerAndName = await _db.Horses
            .Select(h => new { h.Id, h.OwnerId, h.Name, h.Species, h.Sex, h.BirthYear, h.AgeGroup, h.Identity })
            .ToListAsync(cancellationToken);
        var existingJournals = await _db.JournalEntries
            .Select(j => new { j.HorseId, j.PerformedAt, j.TreatmentTypeName })
            .ToListAsync(cancellationToken);

        var createdOwners = new Dictionary<string, Owner>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < owners.Count; i++)
        {
            var row = owners[i];
            var name = Cell(row, "name");
            var email = Cell(row, "email").ToLowerInvariant();
            var phone = Cell(row, "phone");
            if (string.IsNullOrWhiteSpace(name))
            {
                report.Skipped.Add(new ImportSkip("owner", i + 2, "Saknar namn"));
                continue;
            }
            if (string.IsNullOrWhiteSpace(email))
            {
                report.Skipped.Add(new ImportSkip("owner", i + 2, "Saknar e-post", name));
                continue;
            }

            if (ownerByEmail.ContainsKey(email) || createdOwners.ContainsKey(email))
            {
                report.Duplicates.Add($"Ägare e-post {email}");
                report.Skipped.Add(new ImportSkip("owner", i + 2, "Dubblett e-post", email));
                continue;
            }

            var phoneKey = NormalizePhone(phone);
            if (phoneKey.Length > 0 && ownerByPhone.ContainsKey(phoneKey))
            {
                report.Duplicates.Add($"Ägare telefon {phone}");
                report.Skipped.Add(new ImportSkip("owner", i + 2, "Dubblett telefon", phone));
                continue;
            }

            var owner = new Owner(
                name,
                email,
                NullIfEmpty(phone),
                NullIfEmpty(Cell(row, "address_street")),
                NullIfEmpty(Cell(row, "address_postcode")),
                NullIfEmpty(Cell(row, "address_city")),
                notes: NullIfEmpty(Cell(row, "notes")));
            createdOwners[email] = owner;
            ownerByEmail[email] = owner;
            if (phoneKey.Length > 0)
                ownerByPhone[phoneKey] = owner;
            report.OwnersCreated++;
            if (commit)
                _db.Owners.Add(owner);
        }

        var createdHorses = new List<(Owner Owner, Horse Horse)>();

        for (var i = 0; i < horses.Count; i++)
        {
            var row = horses[i];
            var ownerEmail = Cell(row, "owner_email").ToLowerInvariant();
            var horseName = Cell(row, "name");
            if (string.IsNullOrWhiteSpace(ownerEmail) || string.IsNullOrWhiteSpace(horseName))
            {
                report.Skipped.Add(new ImportSkip("horse", i + 2, "Saknar owner_email eller namn"));
                continue;
            }

            if (!ownerByEmail.TryGetValue(ownerEmail, out var owner))
            {
                report.Skipped.Add(new ImportSkip("horse", i + 2, "Ägare saknas", ownerEmail));
                continue;
            }

            var exists = horseByOwnerAndName.Any(h =>
                h.OwnerId == owner.Id && h.Name.Equals(horseName, StringComparison.OrdinalIgnoreCase));
            exists = exists || createdHorses.Any(h =>
                h.Owner.Email.Equals(ownerEmail, StringComparison.OrdinalIgnoreCase)
                && h.Horse.Name.Equals(horseName, StringComparison.OrdinalIgnoreCase));
            if (exists)
            {
                report.Duplicates.Add($"Häst {horseName} / {ownerEmail}");
                report.Skipped.Add(new ImportSkip("horse", i + 2, "Dubblett hästnamn + ägare", horseName));
                continue;
            }

            var birthYear = ParseInt(Cell(row, "birth_year"));
            var ageGroup = NullIfEmpty(Cell(row, "age_group"));
            if (birthYear is null && ageGroup is null)
            {
                report.Skipped.Add(new ImportSkip("horse", i + 2, "Kräver birth_year eller age_group", horseName));
                continue;
            }

            try
            {
                var horse = new Horse(
                    owner.Id,
                    horseName,
                    ParseSpecies(Cell(row, "species")),
                    NullIfEmpty(Cell(row, "breed")),
                    ParseSex(Cell(row, "sex")),
                    birthYear,
                    ageGroup,
                    NullIfEmpty(Cell(row, "identity")),
                    NullIfEmpty(Cell(row, "colour")),
                    NullIfEmpty(Cell(row, "markings")),
                    stableCity: NullIfEmpty(Cell(row, "stable_city")),
                    background: NullIfEmpty(Cell(row, "background")));
                createdHorses.Add((owner, horse));
                horseByOwnerAndName.Add(new
                {
                    horse.Id,
                    horse.OwnerId,
                    horse.Name,
                    horse.Species,
                    horse.Sex,
                    horse.BirthYear,
                    horse.AgeGroup,
                    horse.Identity
                });
                report.HorsesCreated++;
                if (commit)
                    _db.Horses.Add(horse);
            }
            catch (Exception ex)
            {
                report.Skipped.Add(new ImportSkip("horse", i + 2, ex.Message, horseName));
            }
        }

        Horse? FindHorse(string ownerEmail, string horseName)
        {
            var created = createdHorses.FirstOrDefault(h =>
                h.Owner.Email.Equals(ownerEmail, StringComparison.OrdinalIgnoreCase)
                && h.Horse.Name.Equals(horseName, StringComparison.OrdinalIgnoreCase));
            if (created.Horse is not null)
                return created.Horse;
            if (!ownerByEmail.TryGetValue(ownerEmail, out var owner))
                return null;
            return _db.Horses.Local.FirstOrDefault(h =>
                       h.OwnerId == owner.Id && h.Name.Equals(horseName, StringComparison.OrdinalIgnoreCase))
                   ?? _db.Horses.FirstOrDefault(h =>
                       h.OwnerId == owner.Id && h.Name.ToLower() == horseName.ToLower());
        }

        for (var i = 0; i < journals.Count; i++)
        {
            var row = journals[i];
            var ownerEmail = Cell(row, "owner_email").ToLowerInvariant();
            var horseName = Cell(row, "horse_name");
            if (string.IsNullOrWhiteSpace(ownerEmail) || string.IsNullOrWhiteSpace(horseName))
            {
                report.Skipped.Add(new ImportSkip("journal", i + 2, "Saknar owner_email eller horse_name"));
                continue;
            }

            var horse = FindHorse(ownerEmail, horseName);
            if (horse is null)
            {
                report.Skipped.Add(new ImportSkip("journal", i + 2, "Häst saknas", $"{ownerEmail}/{horseName}"));
                continue;
            }

            if (!ownerByEmail.TryGetValue(ownerEmail, out var owner))
            {
                report.Skipped.Add(new ImportSkip("journal", i + 2, "Ägare saknas", ownerEmail));
                continue;
            }

            if (!TryParseDate(Cell(row, "performed_at"), out var performedAt))
            {
                report.Skipped.Add(new ImportSkip("journal", i + 2, "Ogiltigt datum", Cell(row, "performed_at")));
                continue;
            }

            var treatment = NullIfEmpty(Cell(row, "treatment_name"));
            var duplicate = existingJournals.Any(j =>
                j.HorseId == horse.Id
                && j.PerformedAt.Date == performedAt.Date
                && string.Equals(j.TreatmentTypeName, treatment, StringComparison.OrdinalIgnoreCase));
            if (duplicate)
            {
                report.Duplicates.Add($"Journal {horseName} {performedAt:yyyy-MM-dd} {treatment}");
                report.Skipped.Add(new ImportSkip("journal", i + 2, "Dubblett journal", $"{horseName} {performedAt:yyyy-MM-dd}"));
                continue;
            }

            var snapshot = JsonSerializer.Serialize(new
            {
                owner.Name,
                owner.Email,
                owner.Phone,
                owner.AddressStreet,
                owner.AddressPostcode,
                owner.AddressCity
            });

            var journal = new JournalEntry(
                horse.Id,
                snapshot,
                horse.Species.ToString(),
                horse.Sex.ToString(),
                horse.BirthYear.HasValue ? horse.BirthYear.Value.ToString() : horse.AgeGroup,
                horse.Identity,
                performedAt,
                Cell(row, "anamnes"),
                Cell(row, "status_klinisk"),
                Cell(row, "atgarder"),
                NullIfEmpty(Cell(row, "diagnos")),
                NullIfEmpty(Cell(row, "differentialdiagnoser")),
                NullIfEmpty(Cell(row, "prognos_och_plan")),
                treatmentTypeName: treatment,
                createdBy: actor,
                source: JournalSource.Import);
            journal.SignImported(signedBy, performedAt);
            existingJournals.Add(new { journal.HorseId, journal.PerformedAt, journal.TreatmentTypeName });
            report.JournalsCreated++;
            if (commit)
                _db.JournalEntries.Add(journal);
        }

        if (commit)
            await _db.SaveChangesAsync(cancellationToken);

        return report;
    }

    private static string Cell(IReadOnlyDictionary<string, string> row, string key) =>
        row.TryGetValue(key, out var value) ? value.Trim() : "";

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static string NormalizePhone(string phone) =>
        new string(phone.Where(char.IsDigit).ToArray());

    private static int? ParseInt(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;

    private static bool TryParseDate(string value, out DateTimeOffset result)
    {
        var formats = new[] { "yyyy-MM-dd", "yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ssK" };
        if (DateTimeOffset.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out result))
            return true;
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out result);
    }

    private static Djurslag ParseSpecies(string value) => value.Trim().ToLowerInvariant() switch
    {
        "" or "hast" or "häst" or "horse" => Djurslag.Hast,
        "ko" => Djurslag.Ko,
        "sva" or "svin" => Djurslag.Sva,
        "katt" => Djurslag.Katt,
        "hund" => Djurslag.Hund,
        _ => Djurslag.other
    };

    private static HingstSex ParseSex(string value) => value.Trim().ToLowerInvariant() switch
    {
        "sto" => HingstSex.Sto,
        "valack" => HingstSex.Valack,
        "hingst" => HingstSex.Hingst,
        _ => HingstSex.Okant
    };
}
