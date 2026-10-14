using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace RentalMigration;
public static class PickupSchemaUpgrade
{
 public static async Task ApplyAsync(string source,string target,CancellationToken ct=default)
 {
  RentalDataMigration.Guard(source,true);RentalDataMigration.Guard(target,false);
  await using var db=RentalDataMigration.Rental(target);
  var receipts=await db.Database.SqlQueryRaw<RentalDataMigration.ImportReceipt>("""SELECT "Id","SourceHash","Verified" FROM "RentalMigrationReceipts" """).ToListAsync(ct);
  if(receipts.Count!=1||!receipts[0].Verified)throw new InvalidOperationException("Pickup upgrade requires a verified booking import.");
  var owner=await db.Database.SqlQuery<string>($"""SELECT service_name AS "Value" FROM "__FleetFlowServiceSchema" WHERE version=2""").SingleOrDefaultAsync(ct);
  if(owner!="RentalService")throw new InvalidOperationException("Rental schema ownership differs.");
  await using(var fleet=RentalDataMigration.Fleet(source))
  {
   var ledger=await fleet.Database.SqlQuery<Guid>($"""SELECT "Id" AS "Value" FROM "RentalLedgerMigrationReceipts" WHERE "Verified"=true""").ToListAsync(ct);
   if(ledger.Count!=1||ledger[0]!=receipts[0].Id)throw new InvalidOperationException("Fleet ledger import differs.");
   await fleet.Database.ExecuteSqlRawAsync(FleetService.Api.Data.ReservationSchema.Sql,ct);
  }
  var sql=await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"migrations/rental/003_pickup.sql"),ct);
  var checksum=RentalDataMigration.Hash(sql.Replace("\r\n","\n").TrimEnd()+"\n");
  await using var tx=await db.Database.BeginTransactionAsync(ct);
  await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext('fleetflow-pickup-schema'))",ct);
  var previous=await db.Database.SqlQuery<string>($"""SELECT checksum AS "Value" FROM "__FleetFlowServiceSchema" WHERE version=3""").ToListAsync(ct);
  var previousOwner=await db.Database.SqlQuery<string>($"""SELECT service_name AS "Value" FROM "__FleetFlowServiceSchema" WHERE version=3""").SingleOrDefaultAsync(ct);
  if(previous.Count>0&&previousOwner!="RentalService")throw new InvalidOperationException("Pickup schema ownership differs.");
  if(previous.Count>0&&previous.Single()!=checksum)throw new InvalidOperationException("Pickup schema checksum differs.");
  await db.Database.ExecuteSqlRawAsync(sql,ct);
  await db.Database.ExecuteSqlInterpolatedAsync($"""INSERT INTO "__FleetFlowServiceSchema"(version,service_name,checksum) VALUES(3,'RentalService',{checksum}) ON CONFLICT(version) DO NOTHING""",ct);
  await tx.CommitAsync(ct);
 }
}
