using Microsoft.EntityFrameworkCore;
namespace FleetService.Api.Data;
public static class MaintenancePlanningSchema
{
    public static Task EnsureAsync(FleetDbContext db, CancellationToken ct = default) => db.Database.ExecuteSqlRawAsync(Sql, ct);
    public const string Sql = """
        ALTER TABLE "MaintenanceRecords" ADD COLUMN IF NOT EXISTS "PlannedEndDateTime" timestamptz NULL;
        ALTER TABLE "MaintenanceRecords" ADD COLUMN IF NOT EXISTS "AssignedStaffId" uuid NULL;
        ALTER TABLE "MaintenanceRecords" ADD COLUMN IF NOT EXISTS "AssignedStaffName" varchar(100) NULL;
        CREATE TABLE IF NOT EXISTS "MaintenanceAuditEntries" (
          "Id" uuid PRIMARY KEY, "MaintenanceRecordId" uuid NOT NULL REFERENCES "MaintenanceRecords"("Id") ON DELETE RESTRICT,
          "ActorId" uuid NOT NULL, "OccurredAt" timestamptz NOT NULL, "Action" varchar(40) NOT NULL,
          "Before" text NULL, "After" text NOT NULL);
        CREATE INDEX IF NOT EXISTS "IX_MaintenanceAudit_Record" ON "MaintenanceAuditEntries"("MaintenanceRecordId","OccurredAt");
        CREATE OR REPLACE FUNCTION fleetflow_immutable_maintenance_audit() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN RAISE EXCEPTION 'Maintenance audit entries are append-only'; END; $$;
        DO $$ BEGIN
          IF NOT EXISTS(SELECT 1 FROM pg_trigger WHERE tgname='fleetflow_maintenance_audit_immutable' AND tgrelid='"MaintenanceAuditEntries"'::regclass) THEN
            CREATE TRIGGER fleetflow_maintenance_audit_immutable BEFORE UPDATE OR DELETE ON "MaintenanceAuditEntries"
            FOR EACH ROW EXECUTE FUNCTION fleetflow_immutable_maintenance_audit();
          END IF;
        END; $$;
        """;
}
