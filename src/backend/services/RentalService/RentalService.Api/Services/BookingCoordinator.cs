using System.Data;
using System.Text.Json;
using FleetFlow.Contracts;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;
using FleetService.Api.Messaging.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RentalService.Api.Data;
namespace RentalService.Api.Services;
public sealed class BookingCoordinator(RentalDbContext db,IFleetGateway fleet)
{
 public async Task<BookingResponse> CreateAsync(Guid customer,CreateBookingRequest request,Guid requestKey,CancellationToken ct=default)
 {
  if(customer==Guid.Empty||request.VehicleId==Guid.Empty)throw new BookingRequestException(400,"Customer and vehicle identifiers are required.");
  var start=RentalPeriod.Utc(request.StartDateTime);var end=RentalPeriod.Utc(request.EndDateTime);
  var key=requestKey==Guid.Empty?Guid.NewGuid():requestKey;
  var workflow=await db.Workflows.AsNoTracking().SingleOrDefaultAsync(w=>w.CustomerId==customer&&w.RequestKey==key,ct);
  if(workflow==null)
  {
   if(RentalPeriod.Error(start,end) is {} error)throw new BookingRequestException(400,error);
   workflow=new(){Id=Guid.NewGuid(),CustomerId=customer,RequestKey=key,VehicleId=request.VehicleId,StartDateTime=start,EndDateTime=end,CreatedAt=DateTime.UtcNow,NextAttemptAt=DateTime.UtcNow};
   if(db.Database.IsNpgsql())
   {
    await db.Database.ExecuteSqlInterpolatedAsync($"""
     INSERT INTO "BookingWorkflows" ("Id","CustomerId","RequestKey","VehicleId","StartDateTime","EndDateTime","Phase","CreatedAt","Attempts","NextAttemptAt")
     VALUES ({workflow.Id},{customer},{key},{request.VehicleId},{start},{end},'ReserveRequested',{workflow.CreatedAt},0,{workflow.NextAttemptAt})
     ON CONFLICT ("CustomerId","RequestKey") DO NOTHING
     """,ct);
    workflow=await db.Workflows.AsNoTracking().SingleAsync(w=>w.CustomerId==customer&&w.RequestKey==key,ct);
   }
   else{db.Workflows.Add(workflow);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();}
  }
  if(workflow.VehicleId!=request.VehicleId||workflow.StartDateTime!=start||workflow.EndDateTime!=end)throw new BookingRequestException(409,"Idempotency key was already used for different booking details.");
  await ResumeAsync(workflow.Id,ct);
  return Map(await db.Bookings.AsNoTracking().SingleOrDefaultAsync(b=>b.Id==workflow.Id,ct)??throw new BookingRequestException(503,"Booking is being reconciled. Check My bookings before trying again."));
 }
 public async Task<BookingResponse?> GetAsync(Guid id,CancellationToken ct=default,Guid? customerId=null)
 {var row=await db.Bookings.AsNoTracking().SingleOrDefaultAsync(b=>b.Id==id&&(!customerId.HasValue||b.CustomerId==customerId.Value),ct);if(row==null)return null;var result=Map(row);result.Vehicle=await fleet.VehicleAsync(row.VehicleId,ct);return result;}
 public async Task<IReadOnlyList<BookingResponse>> MineAsync(Guid customer,CancellationToken ct=default)
 {
  var rows=await db.Bookings.AsNoTracking().Where(b=>b.CustomerId==customer).OrderByDescending(b=>b.StartDateTime).ThenByDescending(b=>b.CreatedAt).ToListAsync(ct);
  var vehicles=new Dictionary<Guid,VehicleResponse>();
  foreach(var batch in rows.Select(b=>b.VehicleId).Distinct().Chunk(8))
  {
   var values=await Task.WhenAll(batch.Select(async id=>(id,vehicle:await fleet.VehicleAsync(id,ct))));
   foreach(var value in values)vehicles[value.id]=value.vehicle;
  }
  return rows.Select(b=>{var row=Map(b);row.Vehicle=vehicles[b.VehicleId];return row;}).ToList();
 }
 public async Task<BookingResponse?> CancelAsync(Guid id,Guid customer,CancellationToken ct=default)
 {
  // The intent commits before the remote release. Crashes never lose a cancellation request.
  await using(var tx=await LockAsync(id,ct))
  {
   var booking=await db.Bookings.SingleOrDefaultAsync(b=>b.Id==id&&b.CustomerId==customer,ct);if(booking==null)return null;
   if(booking.Status==BookingStatus.Cancelled)throw new BookingRequestException(409,"This booking has already been cancelled.");
   if(booking.Status is not (BookingStatus.Pending or BookingStatus.Confirmed))throw new BookingRequestException(400,$"A booking in '{booking.Status}' status cannot be cancelled.");
   var flow=await db.Workflows.SingleAsync(w=>w.Id==id,ct);
   flow.Phase="CancelRequested";flow.NextAttemptAt=DateTime.UtcNow;flow.FailureStatus=null;flow.FailureMessage=null;
   await db.SaveChangesAsync(ct);if(tx!=null)await tx.CommitAsync(ct);
  }
  db.ChangeTracker.Clear();await ResumeAsync(id,ct);return Map(await db.Bookings.AsNoTracking().SingleAsync(b=>b.Id==id,ct));
 }
 public async Task ResumeAsync(Guid id,CancellationToken ct=default)
 {
  db.ChangeTracker.Clear();await using var tx=await LockAsync(id,ct);
  var flow=await db.Workflows.SingleAsync(w=>w.Id==id,ct);
  if(flow.Phase=="Rejected")throw new BookingRequestException(flow.FailureStatus??409,flow.FailureMessage??"Booking was rejected.");
  if(flow.Phase=="Done")return;
  flow.Attempts++;
  try
  {
   if(flow.Phase=="ReserveRequested")
   {
    var ack=await fleet.ReserveAsync(new(flow.Id,flow.VehicleId,flow.CustomerId,flow.StartDateTime,flow.EndDateTime),ct);
    if(ack.BookingId!=flow.Id||ack.Released||ack.Vehicle.Id!=flow.VehicleId)throw new BookingRequestException(503,"Fleet returned an incomplete reservation acknowledgement.");
    var booking=new RentalBooking{Id=flow.Id,CustomerId=flow.CustomerId,VehicleId=flow.VehicleId,StartDateTime=flow.StartDateTime,EndDateTime=flow.EndDateTime,
     Status=BookingStatus.Confirmed,TotalCost=ack.TotalCost,CreatedAt=flow.CreatedAt,VehicleSnapshot=JsonSerializer.Serialize(ack.Vehicle)};
    db.Bookings.Add(booking);db.NotificationOutbox.Add(NotificationOutbox.From(new BookingCreatedEvent{EventId=Guid.NewGuid(),OccurredAt=DateTime.UtcNow,BookingId=flow.Id,CustomerId=flow.CustomerId,VehicleId=flow.VehicleId}));
   }
   else if(flow.Phase=="CancelRequested")
   {
    var ack=await fleet.ReleaseAsync(flow.Id,ct);
    if(ack.BookingId!=flow.Id||!ack.Released||ack.Vehicle.Id!=flow.VehicleId)throw new BookingRequestException(503,"Fleet did not acknowledge reservation release.");
    var booking=await db.Bookings.SingleAsync(b=>b.Id==id,ct);booking.Status=BookingStatus.Cancelled;booking.UpdatedAt=DateTime.UtcNow;
    db.NotificationOutbox.Add(NotificationOutbox.From(new BookingCancelledEvent{EventId=Guid.NewGuid(),OccurredAt=booking.UpdatedAt.Value,BookingId=id,CustomerId=flow.CustomerId,VehicleId=flow.VehicleId}));
   }
   else throw new InvalidOperationException("Unknown workflow phase.");
   flow.Phase="Done";flow.FailureStatus=null;flow.FailureMessage=null;
   // Booking + event + workflow completion are one local transaction, after Fleet's durable acknowledgement.
   await db.SaveChangesAsync(ct);if(tx!=null)await tx.CommitAsync(ct);
  }
  catch(BookingRequestException ex)
  {
   if(flow.Phase=="ReserveRequested"&&ex.Status is 400 or 404 or 409)flow.Phase="Rejected";
   flow.FailureStatus=ex.Status;flow.FailureMessage=ex.Message;flow.NextAttemptAt=DateTime.UtcNow.AddSeconds(Math.Min(60,Math.Pow(2,Math.Min(flow.Attempts,6))));
   await db.SaveChangesAsync(ct);if(tx!=null)await tx.CommitAsync(ct);throw;
  }
 }
 private async Task<IDbContextTransaction?> LockAsync(Guid id,CancellationToken ct)
 {
  if(!db.Database.IsRelational())return null;
  var tx=await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted,ct);
  try{await db.Workflows.FromSqlInterpolated($"""SELECT * FROM "BookingWorkflows" WHERE "Id"={id} FOR UPDATE""").AsNoTracking().ToListAsync(ct);return tx;}
  catch{await tx.DisposeAsync();throw;}
 }
 public static BookingResponse Map(RentalBooking b)=>new(){Id=b.Id,CustomerId=b.CustomerId,VehicleId=b.VehicleId,StartDateTime=b.StartDateTime,EndDateTime=b.EndDateTime,Status=b.Status,
  TotalCost=b.TotalCost,CreatedAt=b.CreatedAt,UpdatedAt=b.UpdatedAt,Vehicle=string.IsNullOrWhiteSpace(b.VehicleSnapshot)?null:JsonSerializer.Deserialize<VehicleResponse>(b.VehicleSnapshot)};
}
