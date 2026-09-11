using Equine.Infrastructure.Email;
using Equine.Infrastructure.Tokens;
using Microsoft.AspNetCore.DataProtection;
using Shouldly;
using Xunit;

namespace Equine.UnitTests.Domain;

public class IcsAndTokenTests
{
    [Fact]
    public void Ics_uses_stockholm_timezone()
    {
        var start = new DateTimeOffset(2026, 6, 15, 8, 0, 0, TimeSpan.Zero);
        var end = start.AddMinutes(30);
        var ics = System.Text.Encoding.UTF8.GetString(IcsBuilder.Build(
            Guid.CreateVersion7(), start, end, "Massage", "Stallet 1, 275 31 Sjöbo", "Test"));
        ics.ShouldContain("TZID:Europe/Stockholm");
        ics.ShouldContain("DTSTART;TZID=Europe/Stockholm:20260615T100000");
        ics.ShouldContain("METHOD:REQUEST");
    }

    [Fact]
    public void Token_purpose_binding_rejects_other_purpose_and_expired()
    {
        var provider = new EphemeralDataProtectionProvider();
        var tokens = new PublicBookingTokenService(provider);
        var id = Guid.CreateVersion7();
        var token = tokens.Protect(id, DateTimeOffset.UtcNow.AddMinutes(30), PublicBookingTokenService.VerifyPurpose);

        tokens.TryUnprotect(token, PublicBookingTokenService.VerifyPurpose, out var parsed, out var expired)
            .ShouldBeTrue();
        parsed.ShouldBe(id);
        expired.ShouldBeFalse();

        tokens.TryUnprotect(token, PublicBookingTokenService.ManagePurpose, out _, out _).ShouldBeFalse();

        var old = tokens.Protect(id, DateTimeOffset.UtcNow.AddMinutes(-1), PublicBookingTokenService.VerifyPurpose);
        tokens.TryUnprotect(old, PublicBookingTokenService.VerifyPurpose, out _, out var isExpired).ShouldBeTrue();
        isExpired.ShouldBeTrue();
    }
}
