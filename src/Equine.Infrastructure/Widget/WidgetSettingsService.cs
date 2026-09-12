using Equine.Domain.Entities;
using Equine.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Equine.Infrastructure.Widget;

public sealed class WidgetSettingsService
{
    public const string CacheKey = "widget-settings";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private readonly EquineDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ITenantContext _tenant;

    public WidgetSettingsService(EquineDbContext db, IMemoryCache cache, ITenantContext tenant)
    {
        _db = db;
        _cache = cache;
        _tenant = tenant;
    }

    private string CacheKeyForTenant =>
        _tenant.HasTenant ? $"{CacheKey}:{_tenant.TenantId:N}" : CacheKey;

    public async Task<WidgetSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKeyForTenant, out WidgetSettings? cached) && cached is not null)
            return cached;

        var settings = await _db.WidgetSettings.AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            settings = WidgetSettings.CreateDefault();
            if (_tenant.HasTenant)
            {
                _db.WidgetSettings.Add(settings);
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        _cache.Set(CacheKeyForTenant, settings, CacheTtl);
        return settings;
    }

    public async Task<WidgetSettings> UpdateAsync(
        IReadOnlyList<string>? allowedOrigins,
        bool? showPrices,
        string? bookingTerms,
        string? privacyPolicyUrl,
        int? verificationWindowMinutes,
        int? cancellationNoticeHours,
        string? publicBaseUrl,
        string? contactPhone,
        string? contactEmail,
        CancellationToken cancellationToken = default)
    {
        var settings = await _db.WidgetSettings
            .FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            settings = WidgetSettings.CreateDefault();
            _db.WidgetSettings.Add(settings);
        }

        settings.Update(
            allowedOrigins,
            showPrices,
            bookingTerms,
            privacyPolicyUrl,
            verificationWindowMinutes,
            cancellationNoticeHours,
            publicBaseUrl,
            contactPhone,
            contactEmail);
        await _db.SaveChangesAsync(cancellationToken);
        _cache.Remove(CacheKeyForTenant);
        return settings;
    }
}
