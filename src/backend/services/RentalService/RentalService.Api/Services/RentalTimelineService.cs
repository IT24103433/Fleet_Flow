using FleetFlow.Contracts;
using FleetService.Api.Entities;
using Microsoft.EntityFrameworkCore;
using RentalService.Api.Data;
namespace RentalService.Api.Services;
public sealed class RentalTimelineService(RentalDbContext db)
{
    public async Task<RentalTimelineResponse?> GetAsync(Guid bookingId, Guid? customer, CancellationToken ct = default)
    {
        var booking = await db.Bookings.AsNoTracking().SingleOrDefaultAsync(b => b.Id == bookingId && (!customer.HasValue || b.CustomerId == customer), ct);
        if (booking == null) return null;
        var events = await db.LifecycleEvents.AsNoTracking().Where(e => e.BookingId == bookingId).ToDictionaryAsync(e => e.Kind, ct);
        var pickup = await db.Pickups.AsNoTracking().SingleOrDefaultAsync(p => p.BookingId == bookingId, ct);
        var items = new List<RentalTimelineItem> { new("Created", "Booking created", booking.CreatedAt, "Reservation dates and original estimate recorded.", booking.CustomerId) };
        if (events.TryGetValue("Confirmed", out var confirmation)) items.Add(new("Confirmed", "Booking confirmed", confirmation.OccurredAt, "Vehicle reservation confirmed.", confirmation.ActorId));
        else if (booking.Status is BookingStatus.Confirmed or BookingStatus.Completed || pickup != null)
            items.Add(new("Confirmed", "Booking confirmed", null, "Historical confirmation time was not recorded.", null));
        if (booking.Status == BookingStatus.Cancelled)
        {
            events.TryGetValue("Cancelled", out var cancellation);
            items.Add(new("Cancelled", "Booking cancelled", cancellation?.OccurredAt ?? booking.UpdatedAt,
                cancellation == null ? "Cancelled booking; timestamp is its last recorded update, when available." : "Reservation cancelled and availability released.", booking.CustomerId));
        }
        if (pickup != null)
        {
            items.Add(new("Pickup", pickup.State == RentalLifecycleState.PickupPending ? "Pickup awaiting confirmation" : "Vehicle picked up",
                pickup.PickupAt, $"Odometer {pickup.PickupOdometer} km; fuel {pickup.PickupFuelPercentage}%.", pickup.PickedUpByUserId));
            var returned = await db.Returns.AsNoTracking().SingleOrDefaultAsync(r => r.RentalId == pickup.Id, ct);
            if (returned != null)
            {
                items.Add(new("Return", pickup.State == RentalLifecycleState.CompletionPending ? "Return awaiting confirmation" : "Vehicle returned",
                    returned.ReturnedAt, $"Odometer {returned.Odometer} km; fuel {returned.FuelPercentage}%." + (returned.IsLate ? " Returned after the booked return time." : ""), returned.CompletedByUserId));
                if (events.TryGetValue("Completed", out var completion)) items.Add(new("Completed", "Rental completed", completion.OccurredAt, "Final amount recorded and vehicle availability updated.", completion.ActorId));
            }
        }
        else if (booking.Status == BookingStatus.Completed)
            items.Add(new("Completed", "Historical booking completed", booking.UpdatedAt, "Pickup and return details were not recorded for this booking.", null));
        return new(bookingId, pickup?.Id, items);
    }
}
