using Equine.Infrastructure;
using Equine.Infrastructure.Practice;
using Equine.Infrastructure.Services;
using Equine.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Equine.Api.Features.PublicBookings;

public sealed class PractitionerResolver
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly PractitionerOptions _options;
    private readonly PracticeSettingsService _practice;
    private readonly ITenantContext _tenant;

    public PractitionerResolver(
        UserManager<ApplicationUser> users,
        IOptions<PractitionerOptions> options,
        PracticeSettingsService practice,
        ITenantContext tenant)
    {
        _users = users;
        _options = options.Value;
        _practice = practice;
        _tenant = tenant;
    }

    public async Task<Guid> GetIdAsync(CancellationToken cancellationToken = default)
    {
        if (!_tenant.HasTenant)
            throw new InvalidOperationException("Tenant context is required.");

        if (_options.UserId is Guid configured && configured != Guid.Empty)
        {
            var configuredUser = await _users.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == configured && u.TenantId == _tenant.TenantId, cancellationToken);
            if (configuredUser is not null) return configuredUser.Id;
        }

        var practice = await _practice.GetAsync(cancellationToken);
        var email = !string.IsNullOrWhiteSpace(practice.Email) ? practice.Email : _options.Email;
        if (!string.IsNullOrWhiteSpace(email))
        {
            var byEmail = await _users.FindByEmailAsync(email);
            if (byEmail is not null && byEmail.TenantId == _tenant.TenantId)
                return byEmail.Id;
        }

        var admin = await _users.Users.AsNoTracking()
            .Where(u => u.TenantId == _tenant.TenantId)
            .OrderBy(u => u.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (admin is null)
            throw new InvalidOperationException("No practitioner user is configured.");
        return admin.Id;
    }
}
