using Equine.Domain.Common;
using Equine.Domain.Locations;
using Shouldly;
using Xunit;

namespace Equine.UnitTests.Domain;

public class AddressNormalizationTests
{
    [Theory]
    [InlineData("27531", true)]
    [InlineData("275 31", true)]
    [InlineData("275  31", true)]
    [InlineData("abc12", false)]
    [InlineData("2753", false)]
    [InlineData("275311", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Swedish_postcode_validation(string? input, bool expected) =>
        AddressNormalization.IsValidSwedishPostcode(input).ShouldBe(expected);

    [Fact]
    public void Format_inserts_space() =>
        AddressNormalization.FormatSwedishPostcode("27531").ShouldBe("275 31");

    [Fact]
    public void FormatDisplay_joins_street_postcode_and_city() =>
        AddressNormalization.FormatDisplay("Storgatan 1", "27531", "Sjöbo").ShouldBe("Storgatan 1, 275 31 Sjöbo");
}

public class TreatmentSlugTests
{
    [Fact]
    public void FromName_folds_swedish_and_dashes() =>
        TreatmentSlug.FromName("Hästmassage Sjöbo").ShouldBe("hastmassage-sjobo");

    [Fact]
    public void FromName_empty_falls_back() =>
        TreatmentSlug.FromName("   ").ShouldBe("behandling");
}

public class PublicBookingReferenceTests
{
    [Fact]
    public void Create_has_expected_shape()
    {
        var value = PublicBookingReference.Create();
        value.ShouldStartWith("HJ-");
        value.Length.ShouldBe(9);
    }
}
