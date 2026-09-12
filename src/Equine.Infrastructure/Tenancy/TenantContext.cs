namespace Equine.Infrastructure.Tenancy;

public sealed class TenantContext : ITenantContext
{
    public Guid TenantId { get; private set; }
    public string? Slug { get; private set; }
    public bool HasTenant => TenantId != Guid.Empty;

    public void SetTenant(Guid tenantId, string? slug = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant is required.", nameof(tenantId));
        TenantId = tenantId;
        Slug = slug;
    }

    public void Clear()
    {
        TenantId = Guid.Empty;
        Slug = null;
    }
}
