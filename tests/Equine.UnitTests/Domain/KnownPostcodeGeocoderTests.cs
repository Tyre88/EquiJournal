using Equine.Api.Features.Availability;
using Shouldly;
using Xunit;

namespace Equine.UnitTests.Domain;

public class KnownPostcodeGeocoderTests
{
    [Fact]
    public async Task Maps_sjobo_and_lund_without_network()
    {
        var geocoder = new KnownPostcodeGeocoder();
        var sjobo = await geocoder.GeocodeAsync("275 31");
        sjobo.ShouldBe(KnownPostcodeGeocoder.Sjobo);

        var lund = await geocoder.GeocodeAsync("22100");
        lund.ShouldBe(KnownPostcodeGeocoder.Lund);
    }
}
