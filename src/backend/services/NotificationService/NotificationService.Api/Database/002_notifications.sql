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

CREATE TABLE IF NOT EXISTS "NotificationReceipts" (
    "EventId" uuid PRIMARY KEY,"PayloadHash" varchar(64) NOT NULL,"ProcessedAt" timestamptz NOT NULL
);
CREATE TABLE IF NOT EXISTS "RejectedNotificationEvents" (
    "Id" uuid PRIMARY KEY,"Topic" varchar(200) NOT NULL,"Partition" integer NOT NULL,"Offset" bigint NOT NULL,
    "PayloadHash" varchar(64) NOT NULL,"Payload" text NOT NULL,"ErrorCode" varchar(50) NOT NULL,"CreatedAt" timestamptz NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_RejectedNotificationEvents_Topic_Partition_Offset" ON "RejectedNotificationEvents" ("Topic","Partition","Offset");
CREATE TABLE IF NOT EXISTS "NotificationMigrationReceipts" (
    "Id" uuid PRIMARY KEY,"SourceHash" varchar(64) NOT NULL,"RecordCount" integer NOT NULL,
    "Verified" boolean NOT NULL,"CreatedAt" timestamptz NOT NULL DEFAULT NOW()
);
