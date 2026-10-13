using FleetFlow.ServiceDefaults;
using FleetService.Api.Entities;
using Microsoft.EntityFrameworkCore;
using RentalMigration;
using RentalService.Api.Data;
using RentalService.Api.Services;
namespace RentalService.Tests;
public sealed class MigrationTests
{
 [Theory][InlineData("Host=example.com;Database=fleetflow_rental",false)][InlineData("Host=127.0.0.1;Database=fleetflow_auth",true)]
 public void UnrelatedOrRemoteTargetsAreRefused(string connection,bool legacy)=>Assert.Throws<InvalidOperationException>(()=>RentalDataMigration.Guard(connection,legacy));
 [Fact]public void TamperedExportIsRefused()=>Assert.Throws<InvalidOperationException>(()=>RentalDataMigration.Validate("[]",new string('0',64)));
 [RentalPostgresFact]public async Task ImportPreservesEveryBookingFieldAndOriginalVehicleRelationships()
 {
  await using var f=await RentalPostgresFixture.Create(false);var old=new FleetService.Api.Entities.Booking{Id=Guid.NewGuid(),CustomerId=f.Customer,VehicleId=f.VehicleId,StartDateTime=new(2026,10,1,9,0,0,DateTimeKind.Utc),EndDateTime=new(2026,10,3,9,0,0,DateTimeKind.Utc),CreatedAt=new(2026,9,28,0,0,0,DateTimeKind.Utc),UpdatedAt=new(2026,10,3,10,0,0,DateTimeKind.Utc),Status=BookingStatus.Completed,TotalCost=18000};
  await using(var fleet=f.Fleet(false)){fleet.Bookings.Add(old);await fleet.SaveChangesAsync();}
  var json=await RentalDataMigration.ExportAsync(f.Source,true);var hash=RentalDataMigration.Hash(json);var sql=await RentalPostgresFixture.Schema();
  Assert.False((await RentalDataMigration.ImportAsync(f.Source,f.Target,json,hash,sql,false)).Applied);
  var receipt=await RentalDataMigration.ImportAsync(f.Source,f.Target,json,hash,sql,true);
  Assert.Equal(json,await RentalDataMigration.ExportAsync(f.Target,false));Assert.Equal(json,await RentalDataMigration.ExportAsync(f.Source,true));
  var repeated=await RentalDataMigration.ImportAsync(f.Source,f.Target,json,hash,sql,true);Assert.Equal(receipt.MigrationId,repeated.MigrationId);
  await using(var fleet=f.Fleet(false)){var ledger=await fleet.VehicleReservations.SingleAsync();Assert.Equal(old.Id,ledger.BookingId);Assert.True(ledger.Released);Assert.Equal(old.TotalCost,ledger.TotalCost);Assert.Equal(old.UpdatedAt,ledger.ReleasedAt);}
  var descriptor=new OwnedServiceDescriptor("RentalService",f.TargetName,"RENTAL");
  Assert.True(await new RentalReadiness(new(f.Target,descriptor),new(true,receipt.MigrationId)).IsReadyAsync(default));
  Assert.False(await new RentalReadiness(new(f.Target,descriptor),new(true,Guid.NewGuid())).IsReadyAsync(default));
  await using(var db=f.Rental()){db.Workflows.Add(new(){Id=Guid.NewGuid(),RequestKey=Guid.NewGuid(),CustomerId=f.Customer,VehicleId=f.VehicleId,CreatedAt=DateTime.UtcNow,StartDateTime=DateTime.UtcNow,EndDateTime=DateTime.UtcNow.AddDays(1),NextAttemptAt=DateTime.UtcNow});await db.SaveChangesAsync();}
  await Assert.ThrowsAsync<InvalidOperationException>(()=>RentalDataMigration.ImportAsync(f.Source,f.Target,json,hash,sql,true));
 }
 [RentalPostgresFact]public async Task FailedLedgerImportCannotActivateRentalAndSameExportResumes()
 {
  await using var f=await RentalPostgresFixture.Create(false);
  await using(var fleet=f.Fleet(false)){await fleet.Database.ExecuteSqlRawAsync(FleetService.Api.Data.ReservationSchema.Sql);await fleet.Database.ExecuteSqlRawAsync("""ALTER TABLE "RentalLedgerMigrationReceipts" ADD CONSTRAINT reject_test_receipt CHECK(false)""");}
  var json=await RentalDataMigration.ExportAsync(f.Source,true);var hash=RentalDataMigration.Hash(json);var sql=await RentalPostgresFixture.Schema();
  await Assert.ThrowsAnyAsync<Exception>(()=>RentalDataMigration.ImportAsync(f.Source,f.Target,json,hash,sql,true));
  Guid pending;
  await using(var db=f.Rental()){pending=(await db.Database.SqlQuery<Guid>($"""SELECT "Id" AS "Value" FROM "RentalMigrationReceipts" """ ).SingleAsync());}
  Assert.False(await new RentalReadiness(new(f.Target,new("RentalService",f.TargetName,"RENTAL")),new(true,pending)).IsReadyAsync(default));
  await using(var fleet=f.Fleet(false))await fleet.Database.ExecuteSqlRawAsync("""ALTER TABLE "RentalLedgerMigrationReceipts" DROP CONSTRAINT reject_test_receipt""");
  var receipt=await RentalDataMigration.ImportAsync(f.Source,f.Target,json,hash,sql,true);Assert.Equal(pending,receipt.MigrationId);
  Assert.True(await new RentalReadiness(new(f.Target,new("RentalService",f.TargetName,"RENTAL")),new(true,pending)).IsReadyAsync(default));
 }
 [RentalPostgresFact]public async Task LegacyRollbackRequiresCompletedRecoveryAndPreservesNewHistory()
 {
  await using var f=await RentalPostgresFixture.Create();var request=BookingRecoveryTests.Request(f);Guid id;
  await using(var db=f.Rental()){
   var service=new BookingCoordinator(db,new BookingRecoveryTests.Gateway(f));id=(await service.CreateAsync(f.Customer,request,Guid.NewGuid())).Id;await service.CancelAsync(id,f.Customer);
  }
  var json=await RentalDataMigration.ExportAsync(f.Target,false);var hash=RentalDataMigration.Hash(json);
  await Assert.ThrowsAsync<InvalidOperationException>(()=>RentalDataMigration.ReconcileLegacyAsync(f.Source,f.Target,json,hash));
  await using(var db=f.Rental())await db.NotificationOutbox.ExecuteUpdateAsync(p=>p.SetProperty(e=>e.Status,"Delivered"));
  Assert.Equal(1,await RentalDataMigration.ReconcileLegacyAsync(f.Source,f.Target,json,hash));
  await using(var fleet=f.Fleet(false)){var row=await fleet.Bookings.SingleAsync();Assert.Equal(id,row.Id);Assert.Equal(BookingStatus.Cancelled,row.Status);Assert.Equal(18000,row.TotalCost);Assert.True((await fleet.VehicleReservations.SingleAsync()).Released);}
  var rows=RentalDataMigration.Validate(json,hash);rows[0].TotalCost=1;var changed=RentalDataMigration.Canonical(rows);
  await Assert.ThrowsAsync<InvalidOperationException>(()=>RentalDataMigration.ReconcileLegacyAsync(f.Source,f.Target,changed,RentalDataMigration.Hash(changed)));
 }
}
