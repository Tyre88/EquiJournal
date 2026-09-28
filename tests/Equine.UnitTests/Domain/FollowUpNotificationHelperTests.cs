using Equine.Domain.Entities;
using Equine.Infrastructure;
using Equine.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Equine.UnitTests.Domain;

public class FollowUpNotificationHelperTests
{
    [Fact]
    public async Task BuildPayloadAsync_includes_tenant_slug_in_portal_link()
    {
        var owner = new Owner("Anna", "anna@example.test");
        var horse = new Horse(owner.Id, "Storm", ageGroup: "vuxen");
        typeof(Horse).GetProperty(nameof(Horse.Owner))!.SetValue(horse, owner);

        var options = new DbContextOptionsBuilder<EquineDbContext>()
            .UseInMemoryDatabase(nameof(BuildPayloadAsync_includes_tenant_slug_in_portal_link))
            .Options;
        await using var db = new EquineDbContext(options);

        var payload = await FollowUpNotificationHelper.BuildPayloadAsync(
            horse,
            "Testklinik",
            "https://app.example.test",
            "min-klinik",
            db);

        payload["portalänk"].ShouldBe("https://app.example.test/portal/min-klinik");
        payload["portallank"].ShouldBe("https://app.example.test/portal/min-klinik");
    }
}
