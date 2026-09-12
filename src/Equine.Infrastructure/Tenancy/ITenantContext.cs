namespace Equine.Infrastructure.Tenancy;

public interface ITenantContext
{
    Guid TenantId { get; }
    string? Slug { get; }
    bool HasTenant { get; }
    void SetTenant(Guid tenantId, string? slug = null);
    void Clear();
}
