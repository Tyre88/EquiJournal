using Equine.Domain.Scheduling;

namespace Equine.Api.Features.Bookings;

public sealed class BookingConflictException : Exception
{
    public IReadOnlyList<Slot> Slots { get; }

    public BookingConflictException(IReadOnlyList<Slot> slots)
        : base("Tiden är inte ledig.")
    {
        Slots = slots;
    }
}
