using Equine.Domain.Common;

namespace Equine.Domain.Entities;

public class Tenant : Entity
{
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public TenantStatus Status { get; private set; } = TenantStatus.Active;
    public TenantPlan Plan { get; private set; } = TenantPlan.Free;
    public TenantSubscriptionStatus SubscriptionStatus { get; private set; } = TenantSubscriptionStatus.Free;
    public string? StripeCustomerId { get; private set; }
    public string? StripeSubscriptionId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private Tenant()
    {
    }

    public Tenant(string name, string? slug = null)
    {
        Rename(name, slug);
        Status = TenantStatus.Active;
        Plan = TenantPlan.Free;
        SubscriptionStatus = TenantSubscriptionStatus.Free;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void Rename(string name, string? slug = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        Name = name.Trim();
        var next = TenantSlug.FromName(string.IsNullOrWhiteSpace(slug) ? Name : slug);
        if (TenantSlug.IsReserved(next))
            next = TenantSlug.FromName($"{next}-klinik");
        Slug = next;
    }

    public void Suspend() => Status = TenantStatus.Suspended;

    public void Activate() => Status = TenantStatus.Active;
}
