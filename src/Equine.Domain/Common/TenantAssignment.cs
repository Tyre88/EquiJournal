namespace Equine.Domain.Common;

public static class TenantAssignment
{
    public static Guid Apply(Guid current, Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant is required.", nameof(tenantId));
        if (current != Guid.Empty && current != tenantId)
            throw new InvalidOperationException("Tenant cannot be changed.");
        return tenantId;
    }
}
