using Equine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Equine.Infrastructure.Widget;

public sealed class WidgetSettingsService
{
    public const string CacheKey = "widget-settings";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private readonly EquineDbContext _db;
    private readonly IMemoryCache _cache;

    public WidgetSettingsService(EquineDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<WidgetSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out WidgetSettings? cached) && cached is not null)
            return cached;

        var settings = await _db.WidgetSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == WidgetSettings.SingletonId, cancellationToken);
        if (settings is null)
        {
            settings = WidgetSettings.CreateDefault();
            _db.WidgetSettings.Add(settings);
            await _db.SaveChangesAsync(cancellationToken);
        }

        _cache.Set(CacheKey, settings, CacheTtl);
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
            .FirstOrDefaultAsync(s => s.Id == WidgetSettings.SingletonId, cancellationToken);
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
        _cache.Remove(CacheKey);
        return settings;
    }
}
