CREATE TABLE IF NOT EXISTS "Bookings" ("Id" uuid PRIMARY KEY,"CustomerId" uuid NOT NULL,"VehicleId" uuid NOT NULL,"StartDateTime" timestamptz NOT NULL,"EndDateTime" timestamptz NOT NULL,"Status" text NOT NULL,"TotalCost" numeric(18,2) NOT NULL,"CreatedAt" timestamptz NOT NULL,"UpdatedAt" timestamptz NULL,"VehicleSnapshot" text NOT NULL);
CREATE INDEX IF NOT EXISTS "IX_Bookings_CustomerId" ON "Bookings"("CustomerId");
CREATE TABLE IF NOT EXISTS "BookingWorkflows" ("Id" uuid PRIMARY KEY,"CustomerId" uuid NOT NULL,"RequestKey" uuid NOT NULL,"VehicleId" uuid NOT NULL,"StartDateTime" timestamptz NOT NULL,"EndDateTime" timestamptz NOT NULL,"Phase" text NOT NULL,"CreatedAt" timestamptz NOT NULL,"Attempts" integer NOT NULL,"NextAttemptAt" timestamptz NOT NULL,"FailureStatus" integer NULL,"FailureMessage" text NULL);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_BookingWorkflows_CustomerId_RequestKey" ON "BookingWorkflows"("CustomerId","RequestKey");
CREATE INDEX IF NOT EXISTS "IX_BookingWorkflows_Phase_NextAttemptAt" ON "BookingWorkflows"("Phase","NextAttemptAt");
CREATE TABLE IF NOT EXISTS "RentalMigrationReceipts" ("Id" uuid PRIMARY KEY,"SourceHash" text NOT NULL,"RecordCount" integer NOT NULL,"Verified" boolean NOT NULL);
CREATE TABLE IF NOT EXISTS "NotificationOutbox" (
            "Id" uuid PRIMARY KEY, "EventType" varchar(100) NOT NULL, "Payload" text NOT NULL,
            "Status" varchar(20) NOT NULL, "Attempts" integer NOT NULL, "CreatedAt" timestamptz NOT NULL,
            "NextAttemptAt" timestamptz NOT NULL, "LockedUntil" timestamptz NULL, "LeaseToken" uuid NULL,
            "InboxDeliveredAt" timestamptz NULL, "KafkaPublishedAt" timestamptz NULL, "DeliveredAt" timestamptz NULL,
            "LastErrorCode" varchar(50) NULL, "ReplayCount" integer NOT NULL,
            "LastReplayedByUserId" uuid NULL, "LastReplayedAt" timestamptz NULL
        );
        CREATE INDEX IF NOT EXISTS "IX_NotificationOutbox_Status_NextAttemptAt" ON "NotificationOutbox" ("Status","NextAttemptAt");
