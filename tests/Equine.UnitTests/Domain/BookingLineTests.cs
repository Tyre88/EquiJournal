using Equine.Domain.Entities;
using Shouldly;
using Xunit;

namespace Equine.UnitTests.Domain;

public class BookingLineTests
{
    [Fact]
    public void Approve_from_requested_becomes_confirmed()
    {
        var line = Line(BookingStatus.Requested);
        line.Approve();
        line.Status.ShouldBe(BookingStatus.Confirmed);
        line.BlocksSlot.ShouldBeTrue();
    }

    [Fact]
    public void Complete_from_confirmed_sets_journal()
    {
        var line = Line(BookingStatus.Confirmed);
        var journalId = Guid.CreateVersion7();
        line.Complete(journalId);
        line.Status.ShouldBe(BookingStatus.Completed);
        line.JournalEntryId.ShouldBe(journalId);
        line.BlocksSlot.ShouldBeFalse();
    }

    [Fact]
    public void Cancel_from_confirmed_records_reason()
    {
        var line = Line(BookingStatus.Confirmed);
        var actor = Guid.CreateVersion7();
        line.Cancel("sjuk", actor);
        line.Status.ShouldBe(BookingStatus.Cancelled);
        line.CancellationReason.ShouldBe("sjuk");
        line.CancelledBy.ShouldBe(actor);
        line.BlocksSlot.ShouldBeFalse();
    }

    [Fact]
    public void Illegal_transitions_are_rejected()
    {
        Should.Throw<InvalidOperationException>(() => Line(BookingStatus.Confirmed).Approve());
        Should.Throw<InvalidOperationException>(() => Line(BookingStatus.Requested).Complete(Guid.CreateVersion7()));
        Should.Throw<InvalidOperationException>(() => Line(BookingStatus.Requested).MarkNoShow());
        Should.Throw<InvalidOperationException>(() => Line(BookingStatus.Completed).Cancel("x", null));
        Should.Throw<InvalidOperationException>(() => Line(BookingStatus.Cancelled).Approve());
    }

    [Fact]
    public void Visit_occupies_slot_only_while_a_line_blocks()
    {
        var visit = new Visit(Guid.CreateVersion7(), Guid.CreateVersion7(), DateTimeOffset.UtcNow);
        var a = Line(BookingStatus.Confirmed, visit.Id);
        visit.AddLine(a);
        visit.OccupiesSlot.ShouldBeTrue();

        a.Complete(Guid.CreateVersion7());
        visit.RefreshOccupancy();
        visit.OccupiesSlot.ShouldBeFalse();
    }

    private static BookingLine Line(BookingStatus status, Guid? visitId = null) =>
        new(
            visitId ?? Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "Massage",
            45,
            800,
            0.25m,
            status,
            BookingSource.Manual,
            0);
}
