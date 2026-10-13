CREATE TABLE IF NOT EXISTS "RentalPickups" (
 "Id" uuid PRIMARY KEY,"BookingId" uuid NOT NULL UNIQUE REFERENCES "Bookings"("Id") ON DELETE RESTRICT,
 "State" text NOT NULL,"PickupAt" timestamptz NOT NULL,"PickupOdometer" integer NOT NULL CHECK("PickupOdometer">=0),
 "PickupFuelPercentage" integer NOT NULL CHECK("PickupFuelPercentage" BETWEEN 0 AND 100),
 "PickupCondition" varchar(2000) NOT NULL,"PickedUpByUserId" uuid NOT NULL);
