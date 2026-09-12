using Equine.Domain.Entities;
using Equine.Infrastructure;
using Equine.Infrastructure.Audit;
using Equine.Infrastructure.Scheduling;
using Equine.Infrastructure.Tenancy;
using Equine.Infrastructure.Widget;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Equine.Jobs;

public sealed class ExpireUnverifiedBookingsService
{
    private readonly EquineDbContext _db;
    private readonly WidgetSettingsService _settings;
    private readonly IAuditWriter _audit;
    private readonly ISlotCache _cache;
    private readonly ILogger<ExpireUnverifiedBookingsService> _log;
    private readonly ITenantContext _tenant;
    private readonly TenantProvisioningService _provisioning;

    public ExpireUnverifiedBookingsService(
        EquineDbContext db,
        WidgetSettingsService settings,
        IAuditWriter audit,
        ISlotCache cache,
        ILogger<ExpireUnverifiedBookingsService> log,
        ITenantContext tenant,
        TenantProvisioningService provisioning)
    {
        _db = db;
        _settings = settings;
        _audit = audit;
        _cache = cache;
        _log = log;
        _tenant = tenant;
        _provisioning = provisioning;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        if (!_tenant.HasTenant)
        {
            var total = 0;
            await _provisioning.ForEachActiveAsync(async (_, ct) =>
            {
                total += await RunForCurrentTenantAsync(ct);
            }, cancellationToken);
            return total;
        }

        return await RunForCurrentTenantAsync(cancellationToken);
    }

    private async Task<int> RunForCurrentTenantAsync(CancellationToken cancellationToken)
    {
        var settings = await _settings.GetAsync(cancellationToken);
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-settings.VerificationWindowMinutes);

        var stale = await _db.BookingLines
            .Include(l => l.Visit).ThenInclude(v => v.Lines)
            .Where(l => l.Source == BookingSource.Widget
                        && l.Status == BookingStatus.Requested
                        && l.EmailVerifiedAt == null
                        && l.CreatedAt <= cutoff)
            .ToListAsync(cancellationToken);

        foreach (var line in stale)
        {
            line.Cancel("VerificationExpired", null);
            line.Visit.RecalculateTiming();
            await _audit.WriteAsync("system", "PUBLIC_BOOKING_EXPIRE", nameof(BookingLine), line.Id.ToString(), BookingStatus.Requested.ToString(), BookingStatus.Cancelled.ToString(), cancellationToken);
        }

        if (stale.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            _cache.InvalidateAll();
            _log.LogInformation("Expired {Count} unverified public bookings", stale.Count);
        }

        return stale.Count;
    }
}
