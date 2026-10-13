using Microsoft.EntityFrameworkCore;
namespace FleetService.Api.Data;
public static class ReservationSchema
{
 public const string Sql="""
 CREATE TABLE IF NOT EXISTS "VehicleReservations" (
 "BookingId" uuid PRIMARY KEY, "VehicleId" uuid NOT NULL REFERENCES "Vehicles"("Id") ON DELETE RESTRICT,
 "CustomerId" uuid NOT NULL,"StartDateTime" timestamptz NOT NULL,"EndDateTime" timestamptz NOT NULL,
 "Released" boolean NOT NULL,"TotalCost" numeric(18,2) NOT NULL,"VehicleSnapshot" text NOT NULL,
 "CreatedAt" timestamptz NOT NULL,"ReleasedAt" timestamptz NULL,
 CONSTRAINT "ReservationPeriod" CHECK ("EndDateTime">"StartDateTime"));
 CREATE INDEX IF NOT EXISTS "IX_VehicleReservations_VehicleId" ON "VehicleReservations"("VehicleId");
 CREATE TABLE IF NOT EXISTS "RentalLedgerMigrationReceipts" ("Id" uuid PRIMARY KEY,"SourceHash" text NOT NULL,"RecordCount" integer NOT NULL,"Verified" boolean NOT NULL);
 """;
 public static Task EnsureAsync(FleetDbContext db,CancellationToken ct=default)=>db.Database.ExecuteSqlRawAsync(Sql,ct);
}
