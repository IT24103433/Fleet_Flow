using FleetFlow.Contracts;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;
using FleetService.Api.Exceptions;
using FleetService.Api.Services;
using Microsoft.EntityFrameworkCore;
using RentalMigration;
using RentalService.Api.Services;
namespace RentalService.Tests;
public sealed class BookingRecoveryTests
{
 internal sealed class Gateway(RentalPostgresFixture fixture,bool loseReserve=false,bool loseRelease=false):IFleetGateway
 {
  private bool reserveLost,releaseLost;
  public async Task<ReservationAcknowledgement> ReserveAsync(ReserveVehicleCommand command,CancellationToken ct)
  {
   await using var db=fixture.Fleet();try{
    var ack=await new ReservationService(db,new VehicleService(db)).ReserveAsync(command,ct);
    if(loseReserve&&!reserveLost){reserveLost=true;throw new BookingRequestException(503,"Response lost after Fleet commit.");}return ack;
   }catch(DuplicateException ex){throw new BookingRequestException(409,ex.Message);}catch(ValidationException ex){throw new BookingRequestException(400,ex.Message);}
  }
  public async Task<ReservationAcknowledgement> ReleaseAsync(Guid id,CancellationToken ct)
  {
   await using var db=fixture.Fleet();var ack=await new ReservationService(db,new VehicleService(db)).ReleaseAsync(id,ct)??throw new BookingRequestException(404,"Reservation missing.");
   if(loseRelease&&!releaseLost){releaseLost=true;throw new BookingRequestException(503,"Response lost after release commit.");}return ack;
  }
  public async Task<RentalCompletionAcknowledgement> CompleteAsync(RentalCompletionCommand command,CancellationToken ct){await using var db=fixture.Fleet();try{return await new ReservationService(db,new VehicleService(db)).CompleteAsync(command,ct);}catch(ValidationException ex){throw new BookingRequestException(400,ex.Message);}catch(DuplicateException ex){throw new BookingRequestException(409,ex.Message);}}
  public async Task<RentalHoldAcknowledgement> HoldAsync(RentalHoldCommand command,CancellationToken ct){await using var db=fixture.Fleet();try{return await new ReservationService(db,new VehicleService(db)).HoldAsync(command,ct);}catch(ValidationException ex){throw new BookingRequestException(400,ex.Message);}catch(DuplicateException ex){throw new BookingRequestException(409,ex.Message);}}
  public async Task<VehicleResponse> VehicleAsync(Guid id,CancellationToken ct){await using var db=fixture.Fleet();return await new VehicleService(db).GetVehicleByIdAsync(id)??throw new BookingRequestException(404,"Vehicle missing.");}
  public async Task<bool> AvailableAsync(Guid vehicleId,DateTime start,DateTime end,CancellationToken ct)
  {await using var db=fixture.Fleet();return await new ReservationService(db,new VehicleService(db)).AvailableAsync(vehicleId,start,end,ct);}
 }
 internal static CreateBookingRequest Request(RentalPostgresFixture f)=>new(){VehicleId=f.VehicleId,StartDateTime=DateTime.UtcNow.AddDays(1),EndDateTime=DateTime.UtcNow.AddDays(3)};
 [RentalPostgresFact]public async Task LostReservationResponseRemainsBlockedAndRestartRecoversExactlyOnce()
 {
  await using var f=await RentalPostgresFixture.Create();var request=Request(f);var key=Guid.NewGuid();
  await using(var db=f.Rental())await Assert.ThrowsAsync<BookingRequestException>(()=>new BookingCoordinator(db,new Gateway(f,true)).CreateAsync(f.Customer,request,key));
  await using(var db=f.Rental()){Assert.Empty(await db.Bookings.ToListAsync());Assert.Equal("ReserveRequested",(await db.Workflows.SingleAsync()).Phase);}
  await using(var db=f.Fleet())Assert.False(await new ReservationService(db,new VehicleService(db)).AvailableAsync(f.VehicleId,request.StartDateTime,request.EndDateTime));
  Guid id;
  await using(var db=f.Rental())id=(await new BookingCoordinator(db,new Gateway(f)).CreateAsync(f.Customer,request,key)).Id;
  await using(var db=f.Rental())Assert.Equal(id,(await new BookingCoordinator(db,new Gateway(f)).CreateAsync(f.Customer,request,key)).Id);
  await using(var db=f.Rental()){Assert.Single(await db.Bookings.ToListAsync());Assert.Single(await db.NotificationOutbox.ToListAsync());Assert.Equal("Done",(await db.Workflows.SingleAsync()).Phase);}
  await using(var db=f.Fleet())Assert.Single(await db.VehicleReservations.ToListAsync());
 }
 [RentalPostgresFact]public async Task OutboxFailureDoesNotConfirmBookingOrFreeReservation()
 {
  await using var f=await RentalPostgresFixture.Create();var request=Request(f);Guid id;
  await using(var db=f.Rental()){
   await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "NotificationOutbox" ADD CONSTRAINT reject_test_event CHECK(false)""");
   await Assert.ThrowsAsync<DbUpdateException>(()=>new BookingCoordinator(db,new Gateway(f)).CreateAsync(f.Customer,request,Guid.NewGuid()));
  }
  await using(var db=f.Rental()){
   Assert.Empty(await db.Bookings.ToListAsync());Assert.Empty(await db.NotificationOutbox.ToListAsync());id=(await db.Workflows.SingleAsync()).Id;
   await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "NotificationOutbox" DROP CONSTRAINT reject_test_event""");
   await new BookingCoordinator(db,new Gateway(f)).ResumeAsync(id);
   Assert.Single(await db.Bookings.ToListAsync());Assert.Single(await db.NotificationOutbox.ToListAsync());
  }
  await using(var db=f.Fleet())Assert.False((await db.VehicleReservations.SingleAsync()).Released);
 }
 [RentalPostgresFact]public async Task LostReleaseResponseRecoversCancellationAndPreservesHistory()
 {
  await using var f=await RentalPostgresFixture.Create();var request=Request(f);Guid id;
  await using(var db=f.Rental())id=(await new BookingCoordinator(db,new Gateway(f)).CreateAsync(f.Customer,request,Guid.NewGuid())).Id;
  await using(var db=f.Rental())await Assert.ThrowsAsync<BookingRequestException>(()=>new BookingCoordinator(db,new Gateway(f,false,true)).CancelAsync(id,f.Customer));
  await using(var db=f.Rental()){Assert.Equal(BookingStatus.Confirmed,(await db.Bookings.SingleAsync()).Status);Assert.Equal("CancelRequested",(await db.Workflows.SingleAsync()).Phase);}
  await using(var db=f.Rental()){var service=new BookingCoordinator(db,new Gateway(f));await service.ResumeAsync(id);Assert.Equal(BookingStatus.Cancelled,(await service.GetAsync(id))!.Status);Assert.Equal(2,await db.NotificationOutbox.CountAsync());Assert.Equal(409,(await Assert.ThrowsAsync<BookingRequestException>(()=>service.CancelAsync(id,f.Customer))).Status);}
  await using(var db=f.Fleet()){var row=await db.VehicleReservations.SingleAsync();Assert.True(row.Released);Assert.Equal(id,row.BookingId);}
 }
 [RentalPostgresFact]public async Task ConcurrentBookingRequestsHaveOneWinnerAndRetainRejectedWorkflow()
 {
  await using var f=await RentalPostgresFixture.Create();var request=Request(f);
  async Task<int> Create(){await using var db=f.Rental();try{await new BookingCoordinator(db,new Gateway(f)).CreateAsync(f.Customer,request,Guid.NewGuid());return 201;}catch(BookingRequestException ex){return ex.Status;}}
  var result=await Task.WhenAll(Create(),Create());Assert.Contains(201,result);Assert.Contains(409,result);
  await using var check=f.Rental();Assert.Single(await check.Bookings.ToListAsync());Assert.Single(await check.NotificationOutbox.ToListAsync());Assert.Equal(1,await check.Workflows.CountAsync(w=>w.Phase=="Rejected"));
 }
 [RentalPostgresFact]public async Task ConcurrentRetriesWithSameKeyReturnOneBookingAndOneEvent()
 {
  await using var f=await RentalPostgresFixture.Create();var request=Request(f);var key=Guid.NewGuid();
  async Task<Guid> Create(){await using var db=f.Rental();return (await new BookingCoordinator(db,new Gateway(f)).CreateAsync(f.Customer,request,key)).Id;}
  var result=await Task.WhenAll(Create(),Create());Assert.Equal(result[0],result[1]);await using var check=f.Rental();Assert.Single(await check.Bookings.ToListAsync());Assert.Single(await check.NotificationOutbox.ToListAsync());
 }
 [RentalPostgresFact]public async Task ChangedRequestCannotReuseKeyAndForeignAccountCannotCancel()
 {
  await using var f=await RentalPostgresFixture.Create();var request=Request(f);var key=Guid.NewGuid();await using var db=f.Rental();var service=new BookingCoordinator(db,new Gateway(f));var booking=await service.CreateAsync(f.Customer,request,key);
  request.EndDateTime=request.EndDateTime.AddDays(1);Assert.Equal(409,(await Assert.ThrowsAsync<BookingRequestException>(()=>service.CreateAsync(f.Customer,request,key))).Status);
  Assert.Null(await service.CancelAsync(booking.Id,Guid.NewGuid()));Assert.Equal(BookingStatus.Confirmed,(await service.GetAsync(booking.Id))!.Status);
 }
 [RentalPostgresFact]public async Task ReservationAndMaintenanceRaceUsesSameVehicleLock()
 {
  await using var f=await RentalPostgresFixture.Create();var request=Request(f);
  async Task<bool> Book(){await using var db=f.Rental();try{await new BookingCoordinator(db,new Gateway(f)).CreateAsync(f.Customer,request,Guid.NewGuid());return true;}catch(BookingRequestException){return false;}}
  async Task<bool> Maintain(){await using var db=f.Fleet();try{await new MaintenanceService(db).CreateMaintenanceRecordAsync(f.Customer,new CreateMaintenanceRecordRequest{VehicleId=f.VehicleId,ScheduledDateTime=DateTime.UtcNow.AddDays(1),ServiceInformation="Scheduled service",Cost=1000});return true;}catch(Exception ex)when(ex is DuplicateException or ValidationException){return false;}}
  var result=await Task.WhenAll(Book(),Maintain());Assert.Single(result,v=>v);await using var check=f.Fleet();Assert.False(await check.VehicleReservations.AnyAsync(r=>!r.Released)&&await check.MaintenanceRecords.AnyAsync(r=>r.Status==MaintenanceStatus.SCHEDULED));
 }
 [RentalPostgresFact]public async Task LedgerCommandRetriesKeepPriceAndReleasedIdsCannotBeReused()
 {
  await using var f=await RentalPostgresFixture.Create();var request=Request(f);var command=new ReserveVehicleCommand(Guid.NewGuid(),f.VehicleId,f.Customer,request.StartDateTime,request.EndDateTime);
  await using var db=f.Fleet();var service=new ReservationService(db,new VehicleService(db));var first=await service.ReserveAsync(command);
  var vehicle=await db.Vehicles.SingleAsync();vehicle.DailyRate=25000;await db.SaveChangesAsync();var second=await service.ReserveAsync(command);Assert.Equal(first.TotalCost,second.TotalCost);
  Assert.True((await service.ReleaseAsync(command.BookingId))!.Released);Assert.True((await service.ReleaseAsync(command.BookingId))!.Released);
  await Assert.ThrowsAsync<DuplicateException>(()=>service.ReserveAsync(command));
 }
 [RentalPostgresFact]public async Task RemoteOwnershipGuardsLegacyWriterAndRetirement()
 {
  await using var f=await RentalPostgresFixture.Create();var request=Request(f);
  await using(var db=f.Rental())await new BookingCoordinator(db,new Gateway(f)).CreateAsync(f.Customer,request,Guid.NewGuid());
  await using(var db=f.Fleet()){
   await Assert.ThrowsAsync<ValidationException>(()=>new VehicleService(db).RetireVehicleAsync(f.VehicleId));
   await Assert.ThrowsAsync<RentalUnavailableException>(()=>new BookingService(db,routing:db.RentalRouting).CreateBookingAsync(f.Customer,request));
   db.Bookings.Add(new(){VehicleId=f.VehicleId,CustomerId=f.Customer,StartDateTime=request.StartDateTime,EndDateTime=request.EndDateTime});
   await Assert.ThrowsAsync<RentalUnavailableException>(()=>db.SaveChangesAsync());Assert.Throws<RentalUnavailableException>(()=>db.SaveChanges());
  }
 }
}
