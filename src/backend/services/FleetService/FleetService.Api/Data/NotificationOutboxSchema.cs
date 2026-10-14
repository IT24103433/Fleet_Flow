using Microsoft.EntityFrameworkCore;
namespace FleetService.Api.Data;
public static class NotificationOutboxSchema
{
    public const string Sql = """
        CREATE TABLE IF NOT EXISTS "NotificationOutbox" (
            "Id" uuid PRIMARY KEY, "EventType" varchar(100) NOT NULL, "Payload" text NOT NULL,
            "Status" varchar(20) NOT NULL, "Attempts" integer NOT NULL, "CreatedAt" timestamptz NOT NULL,
            "NextAttemptAt" timestamptz NOT NULL, "LockedUntil" timestamptz NULL, "LeaseToken" uuid NULL,
            "InboxDeliveredAt" timestamptz NULL, "KafkaPublishedAt" timestamptz NULL, "DeliveredAt" timestamptz NULL,
            "LastErrorCode" varchar(50) NULL, "ReplayCount" integer NOT NULL,
            "LastReplayedByUserId" uuid NULL, "LastReplayedAt" timestamptz NULL
        );
        CREATE INDEX IF NOT EXISTS "IX_NotificationOutbox_Status_NextAttemptAt" ON "NotificationOutbox" ("Status","NextAttemptAt");
        """;
    public static Task EnsureAsync(FleetDbContext db,CancellationToken ct=default)=>db.Database.ExecuteSqlRawAsync(Sql,ct);
}
