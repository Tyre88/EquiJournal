using Equine.Domain.Scheduling;
using Shouldly;
using Xunit;

namespace Equine.UnitTests.Domain;

public class SlotEngineThursdayTests
{
    [Fact]
    public void Thursday_2026_09_24_has_slots()
    {
        var day = new DateOnly(2026, 9, 24);
        var query = new SlotQuery(Guid.NewGuid(), Guid.NewGuid(), day, day, null);
        var input = new SlotEngineInput(
            [new SlotRule(DayOfWeek.Thursday, new TimeOnly(8, 0), new TimeOnly(17, 0), null, null, null, true)],
            [],
            [],
            [],
            new SlotTreatment(30, 0, 0, 0, 365),
            new DateTimeOffset(2026, 9, 10, 10, 0, 0, TimeSpan.Zero));
        var slots = SlotEngine.GetAvailableSlots(query, input, new SlotOptions(ApplyMinNotice: false, ApplyMaxAdvance: false));
        slots.Count.ShouldBeGreaterThan(0);
    }
}
