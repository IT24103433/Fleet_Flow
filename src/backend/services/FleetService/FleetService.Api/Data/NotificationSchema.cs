using Microsoft.EntityFrameworkCore;

namespace FleetService.Api.Data;

// Match the repository's idempotent table initialization for databases created before Sprint 3.
public static class NotificationSchema
{
    public static Task EnsureAsync(FleetDbContext db, CancellationToken ct = default) => db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "Notifications" (
            "Id" uuid NOT NULL PRIMARY KEY,
            "EventId" uuid NOT NULL,
            "TargetUserId" uuid NOT NULL,
            "Category" character varying(50) NOT NULL,
            "Title" character varying(200) NOT NULL,
            "Message" character varying(2000) NOT NULL,
            "RelatedEntityId" uuid NULL,
            "VehicleId" uuid NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "IsRead" boolean NOT NULL DEFAULT FALSE
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Notifications_EventId_TargetUserId"
            ON "Notifications" ("EventId", "TargetUserId");
        CREATE INDEX IF NOT EXISTS "IX_Notifications_TargetUserId_IsRead_CreatedAt"
            ON "Notifications" ("TargetUserId", "IsRead", "CreatedAt");
        """, ct);
}
