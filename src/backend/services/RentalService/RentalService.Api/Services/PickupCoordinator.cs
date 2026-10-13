using FleetFlow.Contracts;
using FleetService.Api.Entities;
using FleetService.Api.Messaging.Events;
using Microsoft.EntityFrameworkCore;
using RentalService.Api.Data;
namespace RentalService.Api.Services;
public sealed class PickupCoordinator(RentalDbContext db,IFleetGateway fleet,TimeProvider clock)
{
 public async Task<RentalResponse?> StartAsync(Guid bookingId,Guid actor,PickupRequest request,CancellationToken ct=default)
 {
  if(actor==Guid.Empty||request.Odometer<0||request.FuelPercentage is <0 or >100||string.IsNullOrWhiteSpace(request.Condition)||request.Condition.Trim().Length is <3 or >2000)
   throw new BookingRequestException(400,"Record a valid odometer, fuel percentage and vehicle condition.");
  await using(var tx=await BookingWorkflowLock.BeginAsync(db,bookingId,ct))
  {
   var booking=await db.Bookings.SingleOrDefaultAsync(b=>b.Id==bookingId,ct);if(booking==null)return null;
   var flow=await db.Workflows.SingleAsync(w=>w.Id==bookingId,ct);var existing=await db.Pickups.SingleOrDefaultAsync(p=>p.BookingId==bookingId,ct);
   if(existing?.State==RentalLifecycleState.Active)throw new BookingRequestException(409,"Vehicle pickup has already been recorded.");
   if(existing!=null)
   {
    if(flow.Phase!="PickupRequested"||existing.PickupOdometer!=request.Odometer||existing.PickupFuelPercentage!=request.FuelPercentage||existing.PickupCondition!=request.Condition.Trim())
     throw new BookingRequestException(409,"A pickup request is already being reconciled. Refresh this record before another handover.");
   }
   else
   {
    var now=RentalPeriod.Utc(clock.GetUtcNow().UtcDateTime);
    if(booking.Status!=BookingStatus.Confirmed||flow.Phase!="Done")throw new BookingRequestException(409,"Only a confirmed booking without another pending operation can start a rental.");
    if(now<booking.StartDateTime||now>=booking.EndDateTime)throw new BookingRequestException(400,"Pickup is allowed at or after the booked pickup time and before the booked return time.");
    db.Pickups.Add(new(){Id=Guid.NewGuid(),BookingId=bookingId,PickupAt=now,PickupOdometer=request.Odometer,PickupFuelPercentage=request.FuelPercentage,PickupCondition=request.Condition.Trim(),PickedUpByUserId=actor});
    flow.Phase="PickupRequested";flow.NextAttemptAt=now;flow.FailureStatus=null;flow.FailureMessage=null;await db.SaveChangesAsync(ct);
   }
   if(tx!=null)await tx.CommitAsync(ct);
  }
  db.ChangeTracker.Clear();await ResumeAsync(bookingId,ct);
  var record=await db.Pickups.AsNoTracking().SingleAsync(p=>p.BookingId==bookingId,ct);return await GetAsync(record.Id,null,ct);
 }
 public async Task ResumeAsync(Guid bookingId,CancellationToken ct=default)
 {
  db.ChangeTracker.Clear();await using var tx=await BookingWorkflowLock.BeginAsync(db,bookingId,ct);
  var flow=await db.Workflows.SingleAsync(w=>w.Id==bookingId,ct);if(flow.Phase!="PickupRequested")return;
  var record=await db.Pickups.SingleAsync(p=>p.BookingId==bookingId,ct);flow.Attempts++;
  try
  {
   var ack=await fleet.HoldAsync(new(bookingId,record.Id,record.PickupOdometer,record.PickupAt),ct);
   if(ack.BookingId!=bookingId||ack.RentalId!=record.Id||!ack.Held)throw new BookingRequestException(503,"Vehicle handover could not be confirmed. Refresh the rental before handing over the keys.");
   record.State=RentalLifecycleState.Active;flow.Phase="Done";flow.FailureStatus=null;flow.FailureMessage=null;
   var booking=await db.Bookings.SingleAsync(b=>b.Id==bookingId,ct);booking.UpdatedAt=clock.GetUtcNow().UtcDateTime;
   db.NotificationOutbox.Add(NotificationOutbox.From(new RentalStartedEvent{EventId=Guid.NewGuid(),OccurredAt=record.PickupAt,RentalId=record.Id,BookingId=bookingId,VehicleId=booking.VehicleId,CustomerId=booking.CustomerId}));
   await db.SaveChangesAsync(ct);if(tx!=null)await tx.CommitAsync(ct);
  }
  catch(BookingRequestException ex)
  {
   // Only a definitive pre-commit rejection permits removing an unsuccessful provisional record.
   // Uncertain failures retain the original immutable intent, rental ID and vehicle protection.
   if(ex.Status is 400 or 404 or 409){db.Pickups.Remove(record);flow.Phase="Done";}
   flow.FailureStatus=ex.Status;flow.FailureMessage=ex.Message;flow.NextAttemptAt=clock.GetUtcNow().UtcDateTime.AddSeconds(Math.Min(60,Math.Pow(2,Math.Min(flow.Attempts,6))));
   await db.SaveChangesAsync(ct);if(tx!=null)await tx.CommitAsync(ct);throw;
  }
 }
 public async Task<RentalResponse?> GetAsync(Guid id,Guid? customer,CancellationToken ct=default)
 {
  var record=await db.Pickups.AsNoTracking().SingleOrDefaultAsync(p=>p.Id==id,ct);if(record==null)return null;
  var booking=await db.Bookings.AsNoTracking().SingleOrDefaultAsync(b=>b.Id==record.BookingId&&(!customer.HasValue||b.CustomerId==customer.Value),ct);if(booking==null)return null;
  var response=BookingCoordinator.Map(booking);response.Vehicle=await fleet.VehicleAsync(booking.VehicleId,ct);response.RentalId=record.Id;response.RentalState=record.State;return Map(record,response);
 }
 public async Task<RentalQueueResponse> QueueAsync(string state,int page,int pageSize,CancellationToken ct=default)
 {
  if(state is not ("AwaitingPickup" or "PickupPending" or "Active")||page<1||pageSize is <1 or >100||(long)(page-1)*pageSize>int.MaxValue)throw new BookingRequestException(400,"Invalid rental queue filters.");
  var query=db.Bookings.AsNoTracking().Where(b=>b.Status==BookingStatus.Confirmed);
  query=state=="AwaitingPickup"?query.Where(b=>!db.Pickups.Any(p=>p.BookingId==b.Id)&&db.Workflows.Any(w=>w.Id==b.Id&&w.Phase=="Done")):
   query.Where(b=>db.Pickups.Any(p=>p.BookingId==b.Id&&(state=="Active"?p.State==RentalLifecycleState.Active:p.State==RentalLifecycleState.PickupPending)));
  var count=await query.CountAsync(ct);var rows=await query.OrderBy(b=>b.StartDateTime).ThenBy(b=>b.Id).Skip((page-1)*pageSize).Take(pageSize).ToListAsync(ct);
  var ids=rows.Select(b=>b.Id).ToArray();var pickups=await db.Pickups.AsNoTracking().Where(p=>ids.Contains(p.BookingId)).ToDictionaryAsync(p=>p.BookingId,ct);
  var items=new List<RentalQueueItem>();
  foreach(var b in rows){var booking=BookingCoordinator.Map(b);booking.Vehicle=await fleet.VehicleAsync(b.VehicleId,ct);RentalResponse? record=null;if(pickups.TryGetValue(b.Id,out var p)){booking.RentalId=p.Id;booking.RentalState=p.State;record=Map(p,booking);}items.Add(new(booking,record));}
  return new(items,count,page,pageSize,(int)Math.Ceiling(count/(decimal)pageSize));
 }
 private static RentalResponse Map(RentalPickup p,FleetService.Api.Dtos.BookingResponse booking)=>new(p.Id,p.BookingId,p.State,p.PickupAt,p.PickupOdometer,p.PickupFuelPercentage,p.PickupCondition,p.PickedUpByUserId,booking);
}
