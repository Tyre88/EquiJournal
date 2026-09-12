using Equine.Domain.Entities;
using Equine.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Equine.Infrastructure.Notifications;

public sealed class NotificationSettingsService
{
    private readonly EquineDbContext _db;
    private readonly ITenantContext _tenant;

    public NotificationSettingsService(EquineDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<NotificationSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var row = await _db.NotificationSettings.FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            row = NotificationSettings.CreateDefault();
            if (_tenant.HasTenant)
            {
                _db.NotificationSettings.Add(row);
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        return row;
    }

    public async Task<NotificationSettings> UpdateAsync(
        Dictionary<string, Dictionary<string, bool>>? enabled,
        int? reminderLeadHours,
        TimeOnly? quietStart,
        TimeOnly? quietEnd,
        int? dailySmsCap,
        TimeOnly? morningSummary,
        CancellationToken cancellationToken = default)
    {
        var row = await GetAsync(cancellationToken);
        row.Update(enabled, reminderLeadHours, quietStart, quietEnd, dailySmsCap, morningSummary);
        await _db.SaveChangesAsync(cancellationToken);
        return row;
    }
}
