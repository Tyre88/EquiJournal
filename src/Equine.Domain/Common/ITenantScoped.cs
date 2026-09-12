namespace Equine.Domain.Common;

public interface ITenantScoped
{
    Guid TenantId { get; }

    void AssignTenant(Guid tenantId);
}
