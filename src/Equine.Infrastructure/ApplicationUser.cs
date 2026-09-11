using Microsoft.AspNetCore.Identity;

namespace Equine.Infrastructure;

public class ApplicationUser : IdentityUser<Guid>
{
    public string? DisplayName { get; set; }
    public new bool TwoFactorEnabled { get; set; }
    public string? TotpSecret { get; set; }
    public bool IsFirstLogin { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
