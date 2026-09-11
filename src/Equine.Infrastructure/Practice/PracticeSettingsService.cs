using Equine.Domain.Entities;
using Equine.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Equine.Infrastructure.Practice;

public sealed class PracticeSettingsService
{
    private readonly EquineDbContext _db;
    private readonly PractitionerOptions _fallback;

    public PracticeSettingsService(EquineDbContext db, IOptions<PractitionerOptions> fallback)
    {
        _db = db;
        _fallback = fallback.Value;
    }

    public async Task<PracticeSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var row = await _db.PracticeSettings.FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            row = PracticeSettings.CreateDefault();
            ApplyFallback(row);
            _db.PracticeSettings.Add(row);
            await _db.SaveChangesAsync(cancellationToken);
        }
        else if (string.IsNullOrWhiteSpace(row.Email) && !string.IsNullOrWhiteSpace(_fallback.Email))
        {
            ApplyFallback(row);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return row;
    }

    public async Task<PracticeSettings> UpdateAsync(
        string name,
        string clinic,
        string address,
        string addressStreet,
        string addressPostcode,
        string addressCity,
        decimal? latitude,
        decimal? longitude,
        string phone,
        string email,
        CancellationToken cancellationToken = default)
    {
        var row = await GetAsync(cancellationToken);
        row.Update(name, clinic, address, addressStreet, addressPostcode, addressCity, latitude, longitude, phone, email);
        await _db.SaveChangesAsync(cancellationToken);
        return row;
    }

    private void ApplyFallback(PracticeSettings row)
    {
        var address = string.IsNullOrWhiteSpace(row.Address) ? _fallback.Address : row.Address;
        var street = string.IsNullOrWhiteSpace(row.AddressStreet) ? address : row.AddressStreet;
        row.Update(
            string.IsNullOrWhiteSpace(row.Name) || row.Name == "Behandlare" ? _fallback.Name : row.Name,
            string.IsNullOrWhiteSpace(row.Clinic) || row.Clinic == "HästJournal" ? _fallback.Clinic : row.Clinic,
            address,
            street,
            row.AddressPostcode,
            row.AddressCity,
            row.Latitude ?? _fallback.Latitude,
            row.Longitude ?? _fallback.Longitude,
            string.IsNullOrWhiteSpace(row.Phone) ? _fallback.Phone : row.Phone,
            string.IsNullOrWhiteSpace(row.Email) ? _fallback.Email : row.Email);
    }
}
