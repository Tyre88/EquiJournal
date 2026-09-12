using Microsoft.AspNetCore.Identity;

namespace Equine.Infrastructure;

public class ApplicationUser : IdentityUser<Guid>
{
    public Guid TenantId { get; set; }
    public string? DisplayName { get; set; }
    public new bool TwoFactorEnabled { get; set; }
    public string? TotpSecret { get; set; }
    public bool IsFirstLogin { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? RefreshTokenHash { get; set; }
    public DateTimeOffset? RefreshTokenExpiresAt { get; set; }
}
