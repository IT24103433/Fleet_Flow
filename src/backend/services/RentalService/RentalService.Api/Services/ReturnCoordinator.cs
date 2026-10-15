using FleetFlow.Contracts;
using FleetService.Api.Entities;
using FleetService.Api.Messaging.Events;
using Microsoft.EntityFrameworkCore;
using RentalService.Api.Data;
namespace RentalService.Api.Services;

public sealed class ReturnCoordinator(RentalDbContext db, IFleetGateway fleet, TimeProvider clock)
{
    public async Task<RentalResponse?> CompleteAsync(Guid rentalId, Guid actor, ReturnRequest request, CancellationToken ct = default)
    {
        var condition = request.Condition?.Trim() ?? "";
        var reason = string.IsNullOrWhiteSpace(request.ExtraChargeReason) ? null : request.ExtraChargeReason.Trim();
        if (actor == Guid.Empty || request.Odometer < 0 || request.FuelPercentage is < 0 or > 100 || condition.Length is < 3 or > 2000 ||
            request.ExtraCharges is < 0 or > 1000000000 || decimal.Round(request.ExtraCharges, 2) != request.ExtraCharges ||
            reason?.Length > 500 || request.ExtraCharges > 0 && (reason == null || reason.Length < 3))
            throw new BookingRequestException(400, "Record valid return details and a reason for any additional charges. Amounts must have at most two decimal places.");
        var bookingId = await db.Pickups.AsNoTracking().Where(p => p.Id == rentalId).Select(p => (Guid?)p.BookingId).SingleOrDefaultAsync(ct);
        if (bookingId == null) return null;
        await using (var tx = await BookingWorkflowLock.BeginAsync(db, bookingId.Value, ct))
        {
            var pickup = await db.Pickups.SingleAsync(p => p.Id == rentalId, ct);
            var booking = await db.Bookings.SingleAsync(b => b.Id == bookingId, ct);
            var flow = await db.Workflows.SingleAsync(w => w.Id == bookingId, ct);
            var saved = await db.Returns.SingleOrDefaultAsync(r => r.RentalId == rentalId, ct);
            if (pickup.State == RentalLifecycleState.Completed) throw new BookingRequestException(409, "Vehicle return has already been recorded.");
            if (saved != null)
            {
                if (flow.Phase != "ReturnRequested" || saved.Odometer != request.Odometer || saved.FuelPercentage != request.FuelPercentage ||
                    saved.Condition != condition || saved.ExtraCharges != request.ExtraCharges || saved.ExtraChargeReason != reason)
                    throw new BookingRequestException(409, "A return is awaiting confirmation. Refresh the record before making another request.");
            }
            else
            {
                var now = RentalPeriod.Utc(clock.GetUtcNow().UtcDateTime);
                if (pickup.State != RentalLifecycleState.Active || booking.Status != BookingStatus.Confirmed || flow.Phase != "Done")
                    throw new BookingRequestException(409, "Only an active rental without another pending operation can be completed.");
                if (request.Odometer < pickup.PickupOdometer || now < pickup.PickupAt)
                    throw new BookingRequestException(400, "Return mileage and time cannot precede the recorded pickup.");
                var final = booking.TotalCost + request.ExtraCharges;
                if (final > 9999999999999999.99m) throw new BookingRequestException(400, "Final amount exceeds the supported range.");
                db.Returns.Add(new() { RentalId = rentalId, ReturnedAt = now, Odometer = request.Odometer,
                    FuelPercentage = request.FuelPercentage, Condition = condition, CompletedByUserId = actor,
                    ExtraCharges = request.ExtraCharges, ExtraChargeReason = reason, FinalAmount = final, IsLate = now > booking.EndDateTime });
                pickup.State = RentalLifecycleState.CompletionPending;
                flow.Phase = "ReturnRequested"; flow.NextAttemptAt = now; flow.FailureStatus = null; flow.FailureMessage = null;
                await db.SaveChangesAsync(ct);
            }
            if (tx != null) await tx.CommitAsync(ct);
        }
        db.ChangeTracker.Clear(); await ResumeAsync(bookingId.Value, ct);
        return await new PickupCoordinator(db, fleet, clock).GetAsync(rentalId, null, ct);
    }

    public async Task ResumeAsync(Guid bookingId, CancellationToken ct = default)
    {
        db.ChangeTracker.Clear(); await using var tx = await BookingWorkflowLock.BeginAsync(db, bookingId, ct);
        var flow = await db.Workflows.SingleAsync(w => w.Id == bookingId, ct);
        if (flow.Phase != "ReturnRequested") return;
        var pickup = await db.Pickups.SingleAsync(p => p.BookingId == bookingId, ct);
        var saved = await db.Returns.SingleAsync(r => r.RentalId == pickup.Id, ct);
        flow.Attempts++;
        try
        {
            var ack = await fleet.CompleteAsync(new(bookingId, pickup.Id, saved.Odometer, saved.ReturnedAt), ct);
            if (ack.BookingId != bookingId || ack.RentalId != pickup.Id || !ack.Released)
                throw new BookingRequestException(503, "Vehicle return could not be confirmed. Refresh the rental record before retrying.");
            var booking = await db.Bookings.SingleAsync(b => b.Id == bookingId, ct);
            var confirmedAt = RentalPeriod.Utc(clock.GetUtcNow().UtcDateTime);
            pickup.State = RentalLifecycleState.Completed; booking.Status = BookingStatus.Completed; booking.UpdatedAt = confirmedAt;
            flow.Phase = "Done"; flow.FailureStatus = null; flow.FailureMessage = null;
            db.LifecycleEvents.Add(new() { BookingId = bookingId, Kind = "Completed", OccurredAt = confirmedAt, ActorId = saved.CompletedByUserId });
            db.NotificationOutbox.Add(NotificationOutbox.From(new RentalCompletedEvent { EventId = Guid.NewGuid(), OccurredAt = confirmedAt,
                RentalId = pickup.Id, BookingId = bookingId, VehicleId = booking.VehicleId, CustomerId = booking.CustomerId, FinalRentalAmount = saved.FinalAmount }));
            await db.SaveChangesAsync(ct); if (tx != null) await tx.CommitAsync(ct);
        }
        catch (BookingRequestException ex)
        {
            // A definitive pre-commit rejection can be corrected. Uncertain acknowledgements keep the original intent.
            if (ex.Status is 400 or 404 or 409) { db.Returns.Remove(saved); pickup.State = RentalLifecycleState.Active; flow.Phase = "Done"; }
            flow.FailureStatus = ex.Status; flow.FailureMessage = ex.Message;
            flow.NextAttemptAt = clock.GetUtcNow().UtcDateTime.AddSeconds(Math.Min(60, Math.Pow(2, Math.Min(flow.Attempts, 6))));
            await db.SaveChangesAsync(ct); if (tx != null) await tx.CommitAsync(ct); throw;
        }
    }
    public static RentalReturnResponse Map(RentalReturn r) => new(r.ReturnedAt, r.Odometer, r.FuelPercentage, r.Condition,
        r.CompletedByUserId, r.ExtraCharges, r.ExtraChargeReason, r.FinalAmount, r.IsLate);
}
