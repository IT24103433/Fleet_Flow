using FleetFlow.Contracts;
using FleetService.Api.Entities;
using FleetService.Api.Exceptions;
using FleetService.Api.Services;
using Microsoft.EntityFrameworkCore;
using RentalMigration;
using RentalService.Api.Services;
namespace RentalService.Tests;
public sealed class PickupTests
{
 private sealed class Clock(DateTime now):TimeProvider {public override DateTimeOffset GetUtcNow()=>new(now);}
 private static PickupRequest Request(int odometer=1200)=>new(){Odometer=odometer,FuelPercentage=60,Condition="Small scratch on rear bumper. Interior checked."};
 private static async Task<(Guid Id,DateTime Start,DateTime End)> Book(RentalPostgresFixture f)
 {var request=BookingRecoveryTests.Request(f);await using var db=f.Rental();var row=await new BookingCoordinator(db,new BookingRecoveryTests.Gateway(f)).CreateAsync(f.Customer,request,Guid.NewGuid());return(row.Id,row.StartDateTime,row.EndDateTime);}
 private sealed class LostResponseGateway(RentalPostgresFixture f):IFleetGateway
 {
  private readonly BookingRecoveryTests.Gateway inner=new(f);private bool lost;
  public Task<ReservationAcknowledgement> ReserveAsync(ReserveVehicleCommand c,CancellationToken ct)=>inner.ReserveAsync(c,ct);
  public Task<ReservationAcknowledgement> ReleaseAsync(Guid id,CancellationToken ct)=>inner.ReleaseAsync(id,ct);
  public Task<FleetService.Api.Dtos.VehicleResponse> VehicleAsync(Guid id,CancellationToken ct)=>inner.VehicleAsync(id,ct);
  public Task<bool> AvailableAsync(Guid id,DateTime a,DateTime b,CancellationToken ct)=>inner.AvailableAsync(id,a,b,ct);
  public Task<RentalCompletionAcknowledgement> CompleteAsync(RentalCompletionCommand c,CancellationToken ct)=>inner.CompleteAsync(c,ct);
  public async Task<RentalHoldAcknowledgement> HoldAsync(RentalHoldCommand c,CancellationToken ct){var ack=await inner.HoldAsync(c,ct);if(!lost){lost=true;throw new BookingRequestException(503,"Lost committed pickup response.");}return ack;}
 }
 [RentalPostgresFact]public async Task ConfirmedPickupOwnsRecordHoldsVehicleAndEmitsOneDurableEvent()
 {
  await using var f=await RentalPostgresFixture.Create();var b=await Book(f);var actor=Guid.NewGuid();Guid id;
  await using(var db=f.Rental()){
   var service=new PickupCoordinator(db,new BookingRecoveryTests.Gateway(f),new Clock(b.Start));var record=(await service.StartAsync(b.Id,actor,Request()))!;id=record.Id;
   Assert.Equal(RentalLifecycleState.Active,record.State);Assert.Equal(actor,record.PickedUpByUserId);Assert.Equal(1200,record.PickupOdometer);Assert.Equal(60,record.PickupFuelPercentage);Assert.Equal(b.Start,record.PickupAt);
   Assert.Null(await service.GetAsync(id,Guid.NewGuid()));Assert.NotNull(await service.GetAsync(id,f.Customer));
   Assert.Equal(409,(await Assert.ThrowsAsync<BookingRequestException>(()=>service.StartAsync(b.Id,actor,Request()))).Status);
   Assert.Equal(409,(await Assert.ThrowsAsync<BookingRequestException>(()=>new BookingCoordinator(db,new BookingRecoveryTests.Gateway(f),service).CancelAsync(b.Id,f.Customer))).Status);
   Assert.Equal(id,(await new BookingCoordinator(db,new BookingRecoveryTests.Gateway(f)).GetAsync(b.Id))!.RentalId);
   Assert.Single((await service.QueueAsync("Active",1,25)).Items);Assert.Empty((await service.QueueAsync("AwaitingPickup",1,25)).Items);
   Assert.Single(await db.NotificationOutbox.Where(e=>e.EventType=="RentalStartedEvent").ToListAsync());
  }
  await using(var db=f.Fleet()){
   var row=await db.VehicleReservations.SingleAsync();Assert.True(row.IsHeld);Assert.False(row.Released);Assert.Equal(id,row.HeldRentalId);
   Assert.Equal(VehicleStatus.InUse,(await db.Vehicles.SingleAsync()).Status);Assert.Equal(1200,(await db.Vehicles.SingleAsync()).Mileage);
   await Assert.ThrowsAsync<ValidationException>(()=>new ReservationService(db,new VehicleService(db)).ReleaseAsync(b.Id));
   await Assert.ThrowsAsync<ValidationException>(()=>new VehicleService(db).UpdateVehicleStatusAsync(f.VehicleId,new(){Status=VehicleStatus.Available}));
   await Assert.ThrowsAsync<ValidationException>(()=>new VehicleService(db).RetireVehicleAsync(f.VehicleId));
   var v=await db.Vehicles.SingleAsync();await Assert.ThrowsAsync<ValidationException>(()=>new VehicleService(db).UpdateVehicleAsync(f.VehicleId,new(){Vin=v.Vin,LicensePlate=v.LicensePlate,Make=v.Make,Model=v.Model,Year=v.Year,VehicleCategoryId=v.VehicleCategoryId,DailyRate=v.DailyRate,Transmission=v.Transmission,FuelType=v.FuelType,SeatingCapacity=v.SeatingCapacity,HubLocation=v.HubLocation,Mileage=1199}));
   await Assert.ThrowsAsync<ValidationException>(()=>new ReservationService(db,new VehicleService(db)).ReserveAsync(new(Guid.NewGuid(),f.VehicleId,f.Customer,b.End.AddDays(1),b.End.AddDays(2))));
  }
 }
 [RentalPostgresFact]public async Task PickupCannotPrecedeStartOrReachReturnTimeAndInvalidInputsLeaveNoIntent()
 {
  await using var f=await RentalPostgresFixture.Create();var b=await Book(f);await using var db=f.Rental();
  foreach(var now in new[]{b.Start.AddTicks(-10),b.End,b.End.AddDays(1)})Assert.Equal(400,(await Assert.ThrowsAsync<BookingRequestException>(()=>new PickupCoordinator(db,new BookingRecoveryTests.Gateway(f),new Clock(now)).StartAsync(b.Id,f.Customer,Request()))).Status);
  foreach(var body in new[]{new PickupRequest{Odometer=-1,FuelPercentage=30,Condition="Checked"},new(){Odometer=100,FuelPercentage=101,Condition="Checked"},new(){Odometer=100,FuelPercentage=-1,Condition="Checked"},new(){Odometer=100,FuelPercentage=30,Condition=" "},new(){Odometer=100,FuelPercentage=30,Condition=new string('a',2001)}})
   Assert.Equal(400,(await Assert.ThrowsAsync<BookingRequestException>(()=>new PickupCoordinator(db,new BookingRecoveryTests.Gateway(f),new Clock(b.Start)).StartAsync(b.Id,f.Customer,body))).Status);
  Assert.Empty(await db.Pickups.ToListAsync());Assert.Equal("Done",(await db.Workflows.SingleAsync()).Phase);
 }
 [RentalPostgresFact]public async Task CancelledBookingCannotStartAndQueueFiltersAreValidated()
 {
  await using var f=await RentalPostgresFixture.Create();var b=await Book(f);await using var db=f.Rental();await new BookingCoordinator(db,new BookingRecoveryTests.Gateway(f)).CancelAsync(b.Id,f.Customer);
  var service=new PickupCoordinator(db,new BookingRecoveryTests.Gateway(f),new Clock(b.Start));Assert.Equal(409,(await Assert.ThrowsAsync<BookingRequestException>(()=>service.StartAsync(b.Id,f.Customer,Request()))).Status);
  foreach(var args in new[]{("Unknown",1,25),("Active",0,25),("Active",1,101),("Active",int.MaxValue,100)})Assert.Equal(400,(await Assert.ThrowsAsync<BookingRequestException>(()=>service.QueueAsync(args.Item1,args.Item2,args.Item3))).Status);
  Assert.Null(await service.StartAsync(Guid.NewGuid(),f.Customer,Request()));Assert.Empty(await db.Pickups.ToListAsync());
 }
 [RentalPostgresFact]public async Task LostPickupAcknowledgementKeepsIntentAndRestartRecoversOriginalHandoverExactlyOnce()
 {
  await using var f=await RentalPostgresFixture.Create();var b=await Book(f);var actor=Guid.NewGuid();Guid rental;
  await using(var db=f.Rental()){
   var service=new PickupCoordinator(db,new LostResponseGateway(f),new Clock(b.Start));Assert.Equal(503,(await Assert.ThrowsAsync<BookingRequestException>(()=>service.StartAsync(b.Id,actor,Request()))).Status);
   var pending=await db.Pickups.SingleAsync();rental=pending.Id;Assert.Equal(RentalLifecycleState.PickupPending,pending.State);
   Assert.Equal(409,(await Assert.ThrowsAsync<BookingRequestException>(()=>service.StartAsync(b.Id,actor,Request(1400)))).Status);
   Assert.Equal(409,(await Assert.ThrowsAsync<BookingRequestException>(()=>new BookingCoordinator(db,new BookingRecoveryTests.Gateway(f)).CancelAsync(b.Id,f.Customer))).Status);
  }
  await using(var db=f.Rental()){
   var pickup=new PickupCoordinator(db,new BookingRecoveryTests.Gateway(f),new Clock(b.End.AddDays(1)));await new BookingCoordinator(db,new BookingRecoveryTests.Gateway(f),pickup).ResumeAsync(b.Id);await pickup.ResumeAsync(b.Id);
   var record=(await pickup.GetAsync(rental,f.Customer))!;Assert.Equal(RentalLifecycleState.Active,record.State);Assert.Equal(b.Start,record.PickupAt);Assert.Equal(actor,record.PickedUpByUserId);Assert.Single(await db.NotificationOutbox.Where(e=>e.EventType=="RentalStartedEvent").ToListAsync());
  }
 }
 [RentalPostgresFact]public async Task LowerOdometerIsRejectedBeforeFleetWriteAndCorrectedRequestCanRetry()
 {
  await using var f=await RentalPostgresFixture.Create();var b=await Book(f);await using(var fleet=f.Fleet()){var v=await fleet.Vehicles.SingleAsync();v.Mileage=1500;await fleet.SaveChangesAsync();}
  await using var db=f.Rental();var pickup=new PickupCoordinator(db,new BookingRecoveryTests.Gateway(f),new Clock(b.Start));Assert.Equal(400,(await Assert.ThrowsAsync<BookingRequestException>(()=>pickup.StartAsync(b.Id,f.Customer,Request()))).Status);
  Assert.Empty(await db.Pickups.ToListAsync());Assert.Equal("Done",(await db.Workflows.SingleAsync()).Phase);Assert.Equal(RentalLifecycleState.Active,(await pickup.StartAsync(b.Id,f.Customer,Request(1500)))!.State);
 }
 [RentalPostgresFact]public async Task LocalOutboxFailureLeavesBlockedIntentAndRecoveryCommitsEventAndRentalTogether()
 {
  await using var f=await RentalPostgresFixture.Create();var b=await Book(f);
  await using(var db=f.Rental()){
   await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "NotificationOutbox" ADD CONSTRAINT reject_pickup CHECK("EventType" <> 'RentalStartedEvent')""");
   await Assert.ThrowsAsync<DbUpdateException>(()=>new PickupCoordinator(db,new BookingRecoveryTests.Gateway(f),new Clock(b.Start)).StartAsync(b.Id,f.Customer,Request()));
  }
  await using(var db=f.Rental()){
   Assert.Equal(RentalLifecycleState.PickupPending,(await db.Pickups.SingleAsync()).State);Assert.Equal("PickupRequested",(await db.Workflows.SingleAsync()).Phase);
   await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "NotificationOutbox" DROP CONSTRAINT reject_pickup""");
   await new PickupCoordinator(db,new BookingRecoveryTests.Gateway(f),new Clock(b.Start)).ResumeAsync(b.Id);
   Assert.Equal(RentalLifecycleState.Active,(await db.Pickups.SingleAsync()).State);Assert.Single(await db.NotificationOutbox.Where(e=>e.EventType=="RentalStartedEvent").ToListAsync());
  }
 }
 [RentalPostgresFact]public async Task ConcurrentHandoverRequestsRecordOnlyOnePickup()
 {
  await using var f=await RentalPostgresFixture.Create();var b=await Book(f);
  async Task<int> Start(){await using var db=f.Rental();try{await new PickupCoordinator(db,new BookingRecoveryTests.Gateway(f),new Clock(b.Start)).StartAsync(b.Id,Guid.NewGuid(),Request());return 200;}catch(BookingRequestException ex){return ex.Status;}}
  var results=await Task.WhenAll(Start(),Start());Assert.Contains(200,results);Assert.All(results,s=>Assert.Contains(s,new[]{200,409}));
  await using var check=f.Rental();Assert.Single(await check.Pickups.ToListAsync());Assert.Single(await check.NotificationOutbox.Where(e=>e.EventType=="RentalStartedEvent").ToListAsync());
 }
 [RentalPostgresFact]public async Task PickupAndCancellationRaceHasOneConsistentWinner()
 {
  await using var f=await RentalPostgresFixture.Create();var b=await Book(f);
  async Task<bool> Pickup(){await using var db=f.Rental();try{await new PickupCoordinator(db,new BookingRecoveryTests.Gateway(f),new Clock(b.Start)).StartAsync(b.Id,f.Customer,Request());return true;}catch(BookingRequestException){return false;}}
  async Task<bool> Cancel(){await using var db=f.Rental();try{await new BookingCoordinator(db,new BookingRecoveryTests.Gateway(f)).CancelAsync(b.Id,f.Customer);return true;}catch(BookingRequestException){return false;}}
  Assert.Single(await Task.WhenAll(Pickup(),Cancel()),v=>v);await using var rental=f.Rental();await using var fleet=f.Fleet();var row=await fleet.VehicleReservations.SingleAsync();var booking=await rental.Bookings.SingleAsync();
  Assert.Equal(booking.Status==BookingStatus.Cancelled,row.Released);Assert.Equal(await rental.Pickups.AnyAsync(),row.IsHeld);Assert.False(row.Released&&row.IsHeld);
 }
 [RentalPostgresFact]public async Task SchemaUpgradeIsIdempotentAndLegacyRollbackRefusesPickupHistory()
 {
  await using var f=await RentalPostgresFixture.Create();await PickupSchemaUpgrade.ApplyAsync(f.Source,f.Target);await PickupSchemaUpgrade.ApplyAsync(f.Source,f.Target);var b=await Book(f);
  await using(var db=f.Rental())await new PickupCoordinator(db,new BookingRecoveryTests.Gateway(f),new Clock(b.Start)).StartAsync(b.Id,f.Customer,Request());
  var export=await RentalDataMigration.ExportAsync(f.Target,false);var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>RentalDataMigration.ReconcileLegacyAsync(f.Source,f.Target,export,RentalDataMigration.Hash(export)));Assert.Contains("pickup",error.Message,StringComparison.OrdinalIgnoreCase);
  await using var fleet=f.Fleet(false);Assert.Empty(await fleet.Bookings.ToListAsync());Assert.True((await fleet.VehicleReservations.SingleAsync()).IsHeld);
 }
}
