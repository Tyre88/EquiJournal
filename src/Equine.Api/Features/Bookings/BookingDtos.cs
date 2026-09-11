using Equine.Domain.Entities;

namespace Equine.Api.Features.Bookings;

public static class BookingDtos
{
    public static object ToListItem(BookingLine line)
    {
        var visit = line.Visit;
        var location = visit.Location;
        var starts = visit.DeriveLineStart(line);
        var ends = visit.DeriveLineEnd(line);
        return new
        {
            line.Id,
            VisitId = visit.Id,
            StartsAt = starts,
            EndsAt = ends,
            VisitStartsAt = visit.StartsAt,
            VisitEndsAt = visit.EndsAt,
            line.Status,
            line.Source,
            line.HorseId,
            HorseName = line.Horse?.Name,
            line.OwnerId,
            OwnerName = line.Owner?.Name,
            OwnerPhone = line.Owner?.Phone,
            line.TreatmentTypeId,
            line.TreatmentName,
            TreatmentColour = line.TreatmentType?.Colour,
            line.DurationMinutes,
            line.Price,
            line.VatRate,
            line.ClientNote,
            line.InternalNote,
            line.JournalEntryId,
            line.SortOrder,
            line.EmailVerifiedAt,
            line.PublicReference,
            LocationId = location?.Id,
            LocationName = location?.Name,
            LocationType = location?.Type,
            AddressStreet = location?.AddressStreet,
            AddressPostcode = location?.AddressPostcode,
            AddressCity = location?.AddressCity,
            Latitude = location?.Latitude,
            Longitude = location?.Longitude,
            visit.PractitionerId
        };
    }

    public static object ToVisitDto(Visit visit) => new
    {
        visit.Id,
        visit.PractitionerId,
        visit.LocationId,
        Location = visit.Location is null ? null : new
        {
            visit.Location.Id,
            visit.Location.Name,
            visit.Location.Type,
            visit.Location.AddressStreet,
            visit.Location.AddressPostcode,
            visit.Location.AddressCity,
            visit.Location.Latitude,
            visit.Location.Longitude
        },
        visit.StartsAt,
        visit.EndsAt,
        visit.OccupiesSlot,
        visit.Source,
        Lines = visit.Lines.OrderBy(l => l.SortOrder).Select(ToListItem)
    };
}
