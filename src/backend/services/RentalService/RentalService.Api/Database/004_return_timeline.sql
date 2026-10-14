CREATE TABLE IF NOT EXISTS "RentalReturns" (
 "RentalId" uuid PRIMARY KEY REFERENCES "RentalPickups"("Id") ON DELETE RESTRICT,
 "ReturnedAt" timestamptz NOT NULL, "Odometer" integer NOT NULL CHECK("Odometer">=0),
 "FuelPercentage" integer NOT NULL CHECK("FuelPercentage" BETWEEN 0 AND 100),
 "Condition" varchar(2000) NOT NULL, "CompletedByUserId" uuid NOT NULL,
 "ExtraCharges" numeric(18,2) NOT NULL CHECK("ExtraCharges">=0), "ExtraChargeReason" varchar(500) NULL,
 "FinalAmount" numeric(18,2) NOT NULL CHECK("FinalAmount">=0), "IsLate" boolean NOT NULL);
CREATE TABLE IF NOT EXISTS "RentalLifecycleEvents" (
 "BookingId" uuid NOT NULL REFERENCES "Bookings"("Id") ON DELETE RESTRICT,
 "Kind" varchar(40) NOT NULL, "OccurredAt" timestamptz NOT NULL, "ActorId" uuid NOT NULL,
 PRIMARY KEY("BookingId","Kind"));
