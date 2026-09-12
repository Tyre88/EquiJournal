namespace Equine.Domain.Common;

public abstract class TenantScopedEntity : Entity, ITenantScoped
{
    public Guid TenantId { get; private set; }

    protected TenantScopedEntity()
    {
    }

    protected TenantScopedEntity(Guid id) : base(id)
    {
    }

    public void AssignTenant(Guid tenantId) => TenantId = TenantAssignment.Apply(TenantId, tenantId);
}
