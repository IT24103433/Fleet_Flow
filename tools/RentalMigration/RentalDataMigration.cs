using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FleetService.Api.Data;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;
using FleetService.Api.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RentalService.Api.Data;
namespace RentalMigration;
public sealed record MigrationResult(Guid MigrationId,int RecordCount,string SourceHash,bool Applied);
public static class RentalDataMigration
{
 public static string Hash(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
 public static string Canonical(IEnumerable<RentalBooking> rows)=>JsonSerializer.Serialize(rows.OrderBy(b=>b.Id));
 public static void Guard(string connection,bool legacy)
 {
  var c=new NpgsqlConnectionStringBuilder(connection);
  if(c.Host is not ("localhost" or "127.0.0.1" or "::1") || c.Database is null ||
   (legacy?c.Database!="fleetflow_fleet"&&!c.Database.StartsWith("fleetflow_hardening_",StringComparison.Ordinal):c.Database!="fleetflow_rental"&&!c.Database.StartsWith("fleetflow_rental_foundation_test_",StringComparison.Ordinal)))
    throw new InvalidOperationException("Rental migration is restricted to explicitly named loopback databases.");
 }
 public static FleetDbContext Fleet(string connection)=>new(new DbContextOptionsBuilder<FleetDbContext>().UseNpgsql(connection).Options);
 public static RentalDbContext Rental(string connection)=>new(new DbContextOptionsBuilder<RentalDbContext>().UseNpgsql(connection).Options);
 public static async Task<string> ExportAsync(string connection,bool legacy,CancellationToken ct=default)
 {
  Guard(connection,legacy);
  if(!legacy){await using var db=Rental(connection);return Canonical(await db.Bookings.AsNoTracking().ToListAsync(ct));}
  await using var fleet=Fleet(connection);var vehicles=new VehicleService(fleet);var rows=await fleet.Bookings.AsNoTracking().ToListAsync(ct);var snapshots=new Dictionary<Guid,string>();
  foreach(var id in rows.Select(b=>b.VehicleId).Distinct())
  {
   var vehicle=await vehicles.GetVehicleByIdAsync(id)??throw new InvalidOperationException("Missing vehicle relationship.");
   vehicle.Images=vehicle.Images.OrderBy(i=>i.Id).ToList();snapshots[id]=JsonSerializer.Serialize(vehicle);
  }
  return Canonical(rows.Select(b=>new RentalBooking{Id=b.Id,CustomerId=b.CustomerId,VehicleId=b.VehicleId,StartDateTime=b.StartDateTime,EndDateTime=b.EndDateTime,Status=b.Status,TotalCost=b.TotalCost,CreatedAt=b.CreatedAt,UpdatedAt=b.UpdatedAt,VehicleSnapshot=snapshots[b.VehicleId]}));
 }
 public static IReadOnlyList<RentalBooking> Validate(string json,string expectedHash)
 {
  if(expectedHash.Length!=64||!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Hash(json)),Encoding.UTF8.GetBytes(expectedHash.ToLowerInvariant())))throw new InvalidOperationException("Export hash differs.");
  var rows=JsonSerializer.Deserialize<RentalBooking[]>(json)??throw new InvalidOperationException("Invalid booking export.");
  if(rows.Select(b=>b.Id).Distinct().Count()!=rows.Length||rows.Any(b=>b.Id==Guid.Empty||b.CustomerId==Guid.Empty||b.VehicleId==Guid.Empty||
   b.StartDateTime.Kind!=DateTimeKind.Utc||b.EndDateTime.Kind!=DateTimeKind.Utc||b.CreatedAt.Kind!=DateTimeKind.Utc||b.UpdatedAt.HasValue&&b.UpdatedAt.Value.Kind!=DateTimeKind.Utc||b.EndDateTime<=b.StartDateTime||b.TotalCost<0||!Enum.IsDefined(b.Status)||JsonSerializer.Deserialize<VehicleResponse>(b.VehicleSnapshot)?.Id!=b.VehicleId))throw new InvalidOperationException("Invalid booking identifiers, timestamps, status or vehicle references.");
  var active=rows.Where(b=>b.Status is BookingStatus.Pending or BookingStatus.Confirmed).ToList();
  if(active.Any(a=>active.Any(b=>b.Id!=a.Id&&b.VehicleId==a.VehicleId&&b.StartDateTime<a.EndDateTime&&b.EndDateTime>a.StartDateTime)))throw new InvalidOperationException("Source contains overlapping active bookings; resolve under a write pause before migration.");
  return rows;
 }
 private static VehicleReservation Reservation(RentalBooking b)=>new(){BookingId=b.Id,VehicleId=b.VehicleId,CustomerId=b.CustomerId,StartDateTime=b.StartDateTime,EndDateTime=b.EndDateTime,
  Released=b.Status is BookingStatus.Cancelled or BookingStatus.Completed,TotalCost=b.TotalCost,VehicleSnapshot=b.VehicleSnapshot,CreatedAt=b.CreatedAt,ReleasedAt=b.Status is BookingStatus.Cancelled or BookingStatus.Completed?b.UpdatedAt??b.CreatedAt:null};
 private static string Ledger(IEnumerable<VehicleReservation> rows)=>JsonSerializer.Serialize(rows.OrderBy(r=>r.BookingId));
 public static async Task<MigrationResult> ImportAsync(string source,string target,string json,string expectedHash,string sql,bool apply,CancellationToken ct=default)
 {
  Guard(source,true);Guard(target,false);var rows=Validate(json,expectedHash);if(!apply)return new(Guid.Empty,rows.Count,expectedHash,false);
  if(await ExportAsync(source,true,ct)!=Canonical(rows))throw new InvalidOperationException("Working source changed after export; re-export under a write pause.");
  var checksum=Hash(sql.Replace("\r\n","\n").TrimEnd()+"\n");var id=Guid.NewGuid();
  await using var db=Rental(target);
  await using(var tx=await db.Database.BeginTransactionAsync(ct))
  {
   await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext('fleetflow-rental-import'))",ct);
   var owners=await db.Database.SqlQuery<string>($"""SELECT service_name AS "Value" FROM "__FleetFlowServiceSchema" WHERE version=1""").ToListAsync(ct);
   if(owners.SingleOrDefault()!="RentalService")throw new InvalidOperationException("Rental foundation belongs to another service.");
   var previous=await db.Database.SqlQuery<string>($"""SELECT checksum AS "Value" FROM "__FleetFlowServiceSchema" WHERE version=2""").ToListAsync(ct);
   if(previous.Count!=0&&previous.Single()!=checksum)throw new InvalidOperationException("Rental schema checksum differs.");
   await db.Database.ExecuteSqlRawAsync(sql,ct);await db.Database.ExecuteSqlInterpolatedAsync($"""INSERT INTO "__FleetFlowServiceSchema"(version,service_name,checksum) VALUES(2,'RentalService',{checksum}) ON CONFLICT(version) DO NOTHING""",ct);
   var current=await db.Bookings.AsNoTracking().ToListAsync(ct);
   var receipt=await db.Database.SqlQueryRaw<ImportReceipt>("""SELECT "Id","SourceHash","Verified" FROM "RentalMigrationReceipts" """).ToListAsync(ct);
   if(receipt.Count>0)
   {
    if(receipt.Count!=1||receipt[0].SourceHash!=expectedHash||Canonical(current)!=Canonical(rows))throw new InvalidOperationException("Target contains later or different booking writes; refusing overwrite.");
    if(await db.Workflows.CountAsync(ct)!=rows.Count||await db.Workflows.AnyAsync(w=>w.Phase!="Done",ct)||await db.NotificationOutbox.AnyAsync(ct))throw new InvalidOperationException("Later business workflows exist; refusing another import.");
    id=receipt[0].Id;
   }
   else
   {
    if(current.Count>0||await db.Workflows.AnyAsync(ct)||await db.NotificationOutbox.AnyAsync(ct))throw new InvalidOperationException("Rental target is not empty.");
    db.Bookings.AddRange(rows);db.Workflows.AddRange(rows.Select(b=>new BookingWorkflow{Id=b.Id,RequestKey=b.Id,CustomerId=b.CustomerId,VehicleId=b.VehicleId,StartDateTime=b.StartDateTime,EndDateTime=b.EndDateTime,Phase="Done",CreatedAt=b.CreatedAt,NextAttemptAt=b.CreatedAt}));
    await db.SaveChangesAsync(ct);await db.Database.ExecuteSqlInterpolatedAsync($"""INSERT INTO "RentalMigrationReceipts"("Id","SourceHash","RecordCount","Verified") VALUES({id},{expectedHash},{rows.Count},false)""",ct);
   }
   if(Canonical(await db.Bookings.AsNoTracking().ToListAsync(ct))!=Canonical(rows))throw new InvalidOperationException("Booking import checksum differs.");
   await tx.CommitAsync(ct);
  }
  // Deliberately no distributed transaction. Pending receipt cannot activate Rental if the ledger leg fails.
  // Re-running the same export resumes this leg without replacing either side's data.
  await using(var fleet=Fleet(source))
  await using(var tx=await fleet.Database.BeginTransactionAsync(ct))
  {
   await fleet.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext('fleetflow-rental-ledger-import'))",ct);
   await fleet.Vehicles.FromSqlRaw("""SELECT * FROM "Vehicles" ORDER BY "Id" FOR UPDATE""").AsNoTracking().ToListAsync(ct);
   if(await ExportAsync(source,true,ct)!=Canonical(rows))throw new InvalidOperationException("Source changed before ledger import.");
   await fleet.Database.ExecuteSqlRawAsync(ReservationSchema.Sql,ct);
   var current=await fleet.VehicleReservations.AsNoTracking().ToListAsync(ct);var expected=rows.Select(Reservation).ToList();
   if(current.Count>0&&Ledger(current)!=Ledger(expected))throw new InvalidOperationException("Fleet ledger contains different or later reservations; refusing overwrite.");
   if(current.Count==0){fleet.VehicleReservations.AddRange(expected);await fleet.SaveChangesAsync(ct);}
   if(Ledger(await fleet.VehicleReservations.AsNoTracking().ToListAsync(ct))!=Ledger(expected))throw new InvalidOperationException("Ledger checksum differs.");
   var existing=await fleet.Database.SqlQueryRaw<ImportReceipt>("""SELECT "Id","SourceHash","Verified" FROM "RentalLedgerMigrationReceipts" """).ToListAsync(ct);
   if(existing.Count>0&&(existing.Count!=1||existing[0].Id!=id||existing[0].SourceHash!=expectedHash))throw new InvalidOperationException("Ledger receipt differs.");
   await fleet.Database.ExecuteSqlInterpolatedAsync($"""INSERT INTO "RentalLedgerMigrationReceipts"("Id","SourceHash","RecordCount","Verified") VALUES({id},{expectedHash},{rows.Count},true) ON CONFLICT("Id") DO NOTHING""",ct);
   await tx.CommitAsync(ct);
  }
  await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "RentalMigrationReceipts" SET "Verified"=true WHERE "Id"={id} AND "SourceHash"={expectedHash}""",ct);
  await ReturnSchemaUpgrade.ApplyAsync(source,target,ct);
  return new(id,rows.Count,expectedHash,true);
 }
 public sealed class ImportReceipt{public Guid Id{get;set;}public string SourceHash{get;set;}="";public bool Verified{get;set;}}
 public static async Task<int> ReconcileLegacyAsync(string source,string target,string json,string hash,CancellationToken ct=default)
 {
  Guard(source,true);Guard(target,false);var rows=Validate(json,hash);
  await using(var rental=Rental(target))
  {
   if(await rental.Pickups.AnyAsync(ct))throw new InvalidOperationException("Pickup history cannot be represented by the legacy booking owner; preserve Rental and use a lifecycle-aware recovery plan.");
   if(await rental.Workflows.AnyAsync(w=>w.Phase!="Done"&&w.Phase!="Rejected",ct)||await rental.NotificationOutbox.AnyAsync(o=>o.Status!="Delivered",ct))
    throw new InvalidOperationException("Recovery/delivery is incomplete. Resolve workflows and outboxes before legacy reconciliation.");
   if(await ExportAsync(target,false,ct)!=Canonical(rows))throw new InvalidOperationException("Rental changed after export.");
  }
  await using var fleet=Fleet(source);await using var tx=await fleet.Database.BeginTransactionAsync(ct);
  await fleet.Vehicles.FromSqlRaw("""SELECT * FROM "Vehicles" ORDER BY "Id" FOR UPDATE""").AsNoTracking().ToListAsync(ct);
  var ledger=await fleet.VehicleReservations.AsNoTracking().ToListAsync(ct);
  if(rows.Any(b=>!ledger.Any(r=>r.BookingId==b.Id&&r.VehicleId==b.VehicleId&&r.CustomerId==b.CustomerId&&r.StartDateTime==b.StartDateTime&&r.EndDateTime==b.EndDateTime&&r.TotalCost==b.TotalCost&&r.Released==(b.Status is BookingStatus.Cancelled or BookingStatus.Completed))))
   throw new InvalidOperationException("Reservation state differs from Rental; resolve before rollback.");
  foreach(var b in rows)
  {
   var old=await fleet.Bookings.SingleOrDefaultAsync(x=>x.Id==b.Id,ct);
   if(old==null)fleet.Bookings.Add(new(){Id=b.Id,CustomerId=b.CustomerId,VehicleId=b.VehicleId,StartDateTime=b.StartDateTime,EndDateTime=b.EndDateTime,Status=b.Status,TotalCost=b.TotalCost,CreatedAt=b.CreatedAt,UpdatedAt=b.UpdatedAt});
   else
   {
    if(old.CustomerId!=b.CustomerId||old.VehicleId!=b.VehicleId||old.StartDateTime!=b.StartDateTime||old.EndDateTime!=b.EndDateTime||old.TotalCost!=b.TotalCost||old.CreatedAt!=b.CreatedAt||
      old.Status==BookingStatus.Cancelled&&b.Status!=BookingStatus.Cancelled||old.Status==BookingStatus.Completed&&b.Status!=BookingStatus.Completed)
      throw new InvalidOperationException("Legacy history differs; refusing overwrite.");
    old.Status=b.Status;old.UpdatedAt=b.UpdatedAt;
   }
  }
  await fleet.SaveChangesAsync(ct);await tx.CommitAsync(ct);return rows.Count;
 }
}
