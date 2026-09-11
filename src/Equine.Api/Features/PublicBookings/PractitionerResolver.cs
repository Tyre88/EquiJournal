using Equine.Infrastructure;
using Equine.Infrastructure.Practice;
using Equine.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Equine.Api.Features.PublicBookings;

public sealed class PractitionerResolver
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly PractitionerOptions _options;
    private readonly PracticeSettingsService _practice;

    public PractitionerResolver(
        UserManager<ApplicationUser> users,
        IOptions<PractitionerOptions> options,
        PracticeSettingsService practice)
    {
        _users = users;
        _options = options.Value;
        _practice = practice;
    }

    public async Task<Guid> GetIdAsync(CancellationToken cancellationToken = default)
    {
        if (_options.UserId is Guid configured && configured != Guid.Empty)
            return configured;

        var practice = await _practice.GetAsync(cancellationToken);
        var email = !string.IsNullOrWhiteSpace(practice.Email) ? practice.Email : _options.Email;
        if (!string.IsNullOrWhiteSpace(email))
        {
            var byEmail = await _users.FindByEmailAsync(email);
            if (byEmail is not null) return byEmail.Id;
        }

        var admin = await _users.Users.AsNoTracking().OrderBy(u => u.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        if (admin is null)
            throw new InvalidOperationException("No practitioner user is configured.");
        return admin.Id;
    }
}
