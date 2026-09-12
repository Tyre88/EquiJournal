using Equine.Domain.Entities;
using Equine.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Equine.Infrastructure.Tenancy;

public sealed class TenantProvisioningService
{
    private readonly EquineDbContext _db;
    private readonly ITenantContext _tenant;

    public TenantProvisioningService(EquineDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task SeedDefaultsAsync(CancellationToken cancellationToken = default)
    {
        if (!_tenant.HasTenant)
            throw new InvalidOperationException("Tenant context is required to seed defaults.");

        if (!await _db.PracticeSettings.AnyAsync(cancellationToken))
            _db.PracticeSettings.Add(PracticeSettings.CreateDefault());

        if (!await _db.WidgetSettings.AnyAsync(cancellationToken))
            _db.WidgetSettings.Add(WidgetSettings.CreateDefault());

        if (!await _db.NotificationSettings.AnyAsync(cancellationToken))
            _db.NotificationSettings.Add(NotificationSettings.CreateDefault());

        if (!await _db.Zones.AnyAsync(z => z.IsFallback, cancellationToken))
            _db.Zones.Add(new Zone("Övriga Sverige", isFallback: true));

        if (_db.ChangeTracker.HasChanges())
            await _db.SaveChangesAsync(cancellationToken);

        await NotificationTemplateSeeder.SeedAsync(_db, cancellationToken);
    }

    public async Task ForEachActiveAsync(Func<Tenant, CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        var tenants = await _db.Tenants.AsNoTracking()
            .Where(t => t.Status == TenantStatus.Active)
            .ToListAsync(cancellationToken);

        foreach (var tenant in tenants)
        {
            _tenant.SetTenant(tenant.Id, tenant.Slug);
            await action(tenant, cancellationToken);
        }
    }
}
