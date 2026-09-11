using Equine.Domain.Scheduling;
using Shouldly;
using Xunit;

namespace Equine.UnitTests.Domain;

public class DriveLogPlannerTests
{
    private static readonly DriveLogHome Home = new("Hemgatan 1, 222 22 Lund", 55.705, 13.193);

    [Fact]
    public void Same_stable_same_day_is_one_business_trip_and_return_home()
    {
        var day = new DateOnly(2026, 9, 10);
        var trips = DriveLogPlanner.BuildDay(day, Home,
        [
            Stop("stall-a", "Stall A", "A-vägen 1", "09:00", "09:30", "Anna", "Blixten", "Massage"),
            Stop("stall-a", "Stall A", "A-vägen 1", "09:30", "10:00", "Anna", "Stjärna", "Kiropraktik")
        ]);

        trips.Count.ShouldBe(2);
        trips[0].Purpose.ShouldBe(DriveLogPurpose.Business);
        trips[0].ToLabel.ShouldBe("Stall A");
        trips[0].Horses.ShouldBe("Blixten, Stjärna");
        trips[0].Treatments.ShouldContain("Massage");
        trips[0].Treatments.ShouldContain("Kiropraktik");
        trips[1].Purpose.ShouldBe(DriveLogPurpose.ReturnHome);
        trips[1].ToLabel.ShouldBe(SchemaPlaces.HomeLabel);
    }

    [Fact]
    public void Back_to_back_stables_include_leg_between_them_and_return_home()
    {
        var day = new DateOnly(2026, 9, 10);
        var trips = DriveLogPlanner.BuildDay(day, Home,
        [
            Stop("stall-a", "Stall A", "A-vägen 1", "09:00", "09:30", "Anna", "Blixten", "Massage", 55.63, 13.70),
            Stop("stall-b", "Stall B", "B-vägen 2", "10:00", "10:30", "Bo", "Ludde", "Massage", 55.43, 13.82)
        ]);

        trips.Select(t => t.Purpose).ShouldBe([
            DriveLogPurpose.Business,
            DriveLogPurpose.Business,
            DriveLogPurpose.ReturnHome
        ]);
        trips[0].FromLabel.ShouldBe(SchemaPlaces.HomeLabel);
        trips[1].FromLabel.ShouldBe("Stall A");
        trips[1].ToLabel.ShouldBe("Stall B");
        trips[2].FromLabel.ShouldBe("Stall B");
    }

    [Fact]
    public void Long_gap_inserts_return_home_before_next_trip()
    {
        var day = new DateOnly(2026, 9, 10);
        var trips = DriveLogPlanner.BuildDay(day, Home,
        [
            Stop("stall-a", "Stall A", "A-vägen 1", "09:00", "09:30", "Anna", "Blixten", "Massage", 55.63, 13.70),
            Stop("stall-b", "Stall B", "B-vägen 2", "14:00", "14:30", "Bo", "Ludde", "Massage", 55.43, 13.82)
        ]);

        trips.Select(t => t.Purpose).ShouldBe([
            DriveLogPurpose.Business,
            DriveLogPurpose.ReturnHome,
            DriveLogPurpose.Business,
            DriveLogPurpose.ReturnHome
        ]);
        trips[1].FromLabel.ShouldBe("Stall A");
        trips[2].FromLabel.ShouldBe(SchemaPlaces.HomeLabel);
        trips[2].ToLabel.ShouldBe("Stall B");
    }

    [Fact]
    public void Empty_day_has_no_trips()
    {
        DriveLogPlanner.BuildDay(new DateOnly(2026, 9, 10), Home, []).ShouldBeEmpty();
    }

    private static DriveLogStopInput Stop(
        string key,
        string name,
        string address,
        string start,
        string end,
        string owner,
        string horse,
        string treatment,
        double? lat = 55.63,
        double? lon = 13.70) =>
        new(
            DateTimeOffset.Parse($"2026-09-10T{start}:00+02:00"),
            DateTimeOffset.Parse($"2026-09-10T{end}:00+02:00"),
            key,
            name,
            address,
            owner,
            horse,
            treatment,
            lat,
            lon);
}
