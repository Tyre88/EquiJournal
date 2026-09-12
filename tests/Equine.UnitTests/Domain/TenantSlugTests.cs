using Equine.Domain.Common;
using Equine.Domain.Entities;
using Shouldly;
using Xunit;

namespace Equine.UnitTests.Domain;

public class TenantSlugTests
{
    [Fact]
    public void FromName_slugifies_swedish_clinic_name()
    {
        TenantSlug.FromName("Hästen i Sjöbo").ShouldBe("hasten-i-sjobo");
    }

    [Fact]
    public void FromName_avoids_reserved_words()
    {
        TenantSlug.FromName("admin").ShouldBe("admin-klinik");
        TenantSlug.IsReserved("widget").ShouldBeTrue();
    }

    [Fact]
    public void AssignTenant_cannot_be_changed()
    {
        var owner = new Owner("Anna", "anna@ex.se");
        var first = Guid.CreateVersion7();
        owner.AssignTenant(first);
        owner.TenantId.ShouldBe(first);
        Should.Throw<InvalidOperationException>(() => owner.AssignTenant(Guid.CreateVersion7()));
    }
}
