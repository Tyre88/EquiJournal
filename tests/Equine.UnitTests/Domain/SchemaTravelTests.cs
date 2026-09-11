using Equine.Domain.Scheduling;
using Shouldly;
using Xunit;

namespace Equine.UnitTests.Domain;

public class SchemaTravelTests
{
    [Fact]
    public void First_stop_of_the_day_starts_from_home()
    {
        SchemaTravel.Origin(null, null, DateTimeOffset.Parse("2026-09-10T08:00:00+02:00"), "a")
            .ShouldBe(TravelOrigin.Home);
    }

    [Fact]
    public void Back_to_back_stops_use_the_previous_booking()
    {
        var prevEnd = DateTimeOffset.Parse("2026-09-10T10:30:00+02:00");
        var nextStart = DateTimeOffset.Parse("2026-09-10T11:00:00+02:00");
        SchemaTravel.Origin(prevEnd, "stall-a", nextStart, "stall-b")
            .ShouldBe(TravelOrigin.PreviousStop);
    }

    [Fact]
    public void Long_gap_assumes_return_home()
    {
        var prevEnd = DateTimeOffset.Parse("2026-09-10T10:30:00+02:00");
        var nextStart = DateTimeOffset.Parse("2026-09-10T14:00:00+02:00");
        SchemaTravel.Origin(prevEnd, "stall-a", nextStart, "stall-b")
            .ShouldBe(TravelOrigin.Home);
    }

    [Fact]
    public void Same_stable_is_treated_as_previous_stop()
    {
        var prevEnd = DateTimeOffset.Parse("2026-09-10T10:30:00+02:00");
        var nextStart = DateTimeOffset.Parse("2026-09-10T16:00:00+02:00");
        SchemaTravel.Origin(prevEnd, "stall-a", nextStart, "stall-a")
            .ShouldBe(TravelOrigin.PreviousStop);
        SchemaTravel.SamePlace("stall-a", "STALL-A").ShouldBeTrue();
    }

    [Fact]
    public void Haversine_and_drive_estimate_are_sane_for_skane_distances()
    {
        var km = SchemaTravel.HaversineKm(55.705, 13.193, 55.632, 13.707);
        km.ShouldBeInRange(25, 45);
        SchemaTravel.EstimateDriveMinutes(km).ShouldBeGreaterThan(20);
    }
}
