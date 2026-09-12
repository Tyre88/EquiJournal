using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Equine.Infrastructure.Services;
using Equine.Infrastructure.Storage;
using Equine.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Equine.Infrastructure.Export;

public interface IArchiveExportService
{
    Task<byte[]> ExportFullArchiveAsync(CancellationToken cancellationToken = default);
    Task<byte[]> ExportAllHorsePdfsAsync(CancellationToken cancellationToken = default);
}

public sealed class ArchiveExportService : IArchiveExportService
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly EquineDbContext _db;
    private readonly IObjectStorage _storage;
    private readonly IPdfExportService _pdf;
    private readonly ITenantContext _tenant;

    public ArchiveExportService(EquineDbContext db, IObjectStorage storage, IPdfExportService pdf, ITenantContext tenant)
    {
        _db = db;
        _storage = storage;
        _pdf = pdf;
        _tenant = tenant;
    }

    public async Task<byte[]> ExportFullArchiveAsync(CancellationToken cancellationToken = default)
    {
        var owners = await _db.Owners.IgnoreQueryFilters().AsNoTracking()
            .Where(o => o.TenantId == _tenant.TenantId).ToListAsync(cancellationToken);
        var horses = await _db.Horses.IgnoreQueryFilters().AsNoTracking()
            .Where(h => h.TenantId == _tenant.TenantId).ToListAsync(cancellationToken);
        var visits = await _db.Visits.AsNoTracking().ToListAsync(cancellationToken);
        var lines = await _db.BookingLines.AsNoTracking().ToListAsync(cancellationToken);
        var journals = await _db.JournalEntries.AsNoTracking().ToListAsync(cancellationToken);
        var amendments = await _db.JournalAmendments.AsNoTracking().ToListAsync(cancellationToken);
        var attachments = await _db.Attachments.AsNoTracking().ToListAsync(cancellationToken);

        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteJson(zip, "manifest.json", new
            {
                format = "hastjournal-archive",
                version = 1,
                exportedAt = DateTimeOffset.UtcNow,
                counts = new
                {
                    owners = owners.Count,
                    horses = horses.Count,
                    visits = visits.Count,
                    bookingLines = lines.Count,
                    journals = journals.Count,
                    amendments = amendments.Count,
                    attachments = attachments.Count
                }
            });
            WriteJson(zip, "owners.json", owners.Select(o => new
            {
                o.Id,
                o.Name,
                o.Email,
                o.Phone,
                o.AddressStreet,
                o.AddressPostcode,
                o.AddressCity,
                o.Notes,
                o.MarketingConsent,
                o.DeletedAt,
                o.CreatedAt
            }));
            WriteJson(zip, "horses.json", horses.Select(h => new
            {
                h.Id,
                h.OwnerId,
                h.Name,
                species = h.Species.ToString(),
                sex = h.Sex.ToString(),
                h.BirthYear,
                h.AgeGroup,
                h.Identity,
                h.Breed,
                h.Colour,
                h.Markings,
                h.StableCity,
                h.Background,
                h.Status,
                h.DeletedAt,
                h.CreatedAt
            }));
            WriteJson(zip, "visits.json", visits.Select(v => new
            {
                v.Id,
                v.PractitionerId,
                v.LocationId,
                v.StartsAt,
                v.EndsAt,
                v.OccupiesSlot,
                source = v.Source.ToString()
            }));
            WriteJson(zip, "booking_lines.json", lines.Select(l => new
            {
                l.Id,
                l.VisitId,
                l.HorseId,
                l.OwnerId,
                l.TreatmentTypeId,
                l.TreatmentName,
                l.DurationMinutes,
                l.Price,
                l.VatRate,
                status = l.Status.ToString(),
                source = l.Source.ToString(),
                l.JournalEntryId,
                l.PublicReference,
                l.CreatedAt
            }));
            WriteJson(zip, "journals.json", journals.Select(j => new
            {
                j.Id,
                j.HorseId,
                j.OwnerSnapshot,
                j.Species,
                j.Sex,
                j.AgeOrAgeGroup,
                j.AnimalIdentity,
                j.PerformedAt,
                j.Anamnes,
                j.StatusKlinisk,
                j.Atgarder,
                j.Diagnos,
                j.Differentialdiagnoser,
                j.PrognosOchPlan,
                j.TreatmentTypeId,
                j.TreatmentTypeName,
                status = j.Status.ToString(),
                source = j.Source.ToString(),
                j.SignedAt,
                j.SignedBy,
                j.ContentHash,
                j.BookingLineId,
                j.CreatedAt
            }));
            WriteJson(zip, "amendments.json", amendments.Select(a => new
            {
                a.Id,
                a.JournalEntryId,
                a.Text,
                a.Reason,
                a.CreatedBy,
                a.CreatedAt
            }));

            var attachmentIndex = new List<object>();
            foreach (var att in attachments)
            {
                var zipPath = $"attachments/{att.Id}/{SafeName(att.OriginalFileName)}";
                var included = false;
                var error = (string?)null;
                try
                {
                    await using var stream = await _storage.GetAsync(att.StorageKey, cancellationToken);
                    var entry = zip.CreateEntry(zipPath);
                    await using var dest = entry.Open();
                    await stream.CopyToAsync(dest, cancellationToken);
                    included = true;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }

                attachmentIndex.Add(new
                {
                    att.Id,
                    att.JournalEntryId,
                    att.StorageKey,
                    att.OriginalFileName,
                    att.ContentType,
                    att.Size,
                    att.Sha256Checksum,
                    zipPath,
                    included,
                    error
                });
            }
            WriteJson(zip, "attachments/index.json", attachmentIndex);
        }

        return output.ToArray();
    }

    public async Task<byte[]> ExportAllHorsePdfsAsync(CancellationToken cancellationToken = default)
    {
        var horses = await _db.Horses
            .IgnoreQueryFilters()
            .Where(h => h.TenantId == _tenant.TenantId)
            .Include(h => h.Owner)
            .Include(h => h.JournalEntries).ThenInclude(j => j.Amendments)
            .Include(h => h.JournalEntries).ThenInclude(j => j.Attachments)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var horse in horses)
            {
                var pdf = await _pdf.ExportHorseJournalHistoryAsync(horse, cancellationToken);
                var entry = zip.CreateEntry($"journals/{SafeName(horse.Name)}_{horse.Id:N}.pdf");
                await using var dest = entry.Open();
                await dest.WriteAsync(pdf, cancellationToken);
            }
        }

        return output.ToArray();
    }

    private static void WriteJson(ZipArchive zip, string name, object value)
    {
        var entry = zip.CreateEntry(name);
        using var stream = entry.Open();
        JsonSerializer.Serialize(stream, value, Json);
    }

    private static string SafeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "unnamed" : cleaned;
    }
}
