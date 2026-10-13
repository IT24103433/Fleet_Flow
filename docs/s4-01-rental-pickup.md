# S4-01: Start rental / vehicle pickup

Admins and Fleet Managers can open Rentals, inspect a confirmed booking and record odometer, fuel percentage and condition before handing over the keys. Pickup is allowed at the booked start time and before the booked return time. The service records the actual UTC pickup timestamp and authenticated staff ID; the browser cannot choose either. Customers see their own handover details from My bookings. Maintenance Staff cannot perform or inspect rental handovers.

Booking status remains Confirmed. A separate lifecycle state is PickupPending or Active; both prevent customer cancellation. The original LKR booking estimate is retained. Vehicle return, final charges and timeline presentation are separate S4-02/S4-03 work. There is deliberately no return action in this PR.

## Durable handover

Rental saves an immutable pending pickup and PickupRequested workflow under the existing booking-row lock. It calls Fleet with that rental ID, timestamp and odometer. Fleet acquires its existing vehicle-row lock, checks the reservation, booking window, current mileage, maintenance and other active handovers, then atomically saves the reservation hold and vehicle InUse status/mileage. Identical internal retries acknowledge the saved hold; changed commands conflict.

Only after Fleet acknowledgement does Rental atomically mark the pickup Active, finish the workflow and save RentalStartedEvent in its durable outbox. A lost response, interrupted request or local commit failure leaves the pending intent for restart recovery. No uncertain handover frees availability. A definitive pre-commit rejection removes only its unsuccessful provisional pickup so the staff member can correct and resubmit. Repeated completed pickups conflict rather than altering history.

An active hold blocks new bookings even beyond the booked return time, vehicle retirement, manual status changes, manual mileage changes and conflicting maintenance. It is not released by cancellation or a timer. This protection remains until the forthcoming return workflow durably acknowledges completion. Pickup and cancellation share the same booking lock; Fleet writes share the same vehicle lock.

## API contracts

| Method / path | Access | Result |
| --- | --- | --- |
| GET `/api/rentals/queue?state=AwaitingPickup&page=1&pageSize=25` | Admin / Fleet Manager | Items containing booking and optional rental, totalCount, page, pageSize, totalPages. States: AwaitingPickup, PickupPending, Active; pageSize 1–100. |
| POST `/api/rentals/bookings/{bookingId}/pickup` | Admin / Fleet Manager | Body: integer odometer (km), integer fuelPercentage (0–100), condition (3–2,000 trimmed characters). 200 only after durable handover; 400 invalid/time/mileage, 404 missing, 409 repeated/conflicting operation, 503 uncertain/unavailable. |
| GET `/api/rentals/{rentalId}` | Owner customer / Admin / Fleet Manager | id, bookingId, state, pickupAt, pickupOdometer, pickupFuelPercentage, pickupCondition, pickedUpByUserId, booking. Foreign customer records return 404 before Fleet is contacted. |
| POST `/internal/reservations/{bookingId}/pickup` | Service token with fleet.reservations.write | bookingId, rentalId, odometer, pickupAt; acknowledgement contains bookingId, rentalId, held. Browser JWTs cannot invoke this command. |

Existing customer booking responses add nullable rentalId/rentalState only when a pickup exists. Legacy responses omit them. Existing booking paths/statuses, role checks and forced-password-change behavior remain. Fleet forwards the new rental paths only in explicit Remote mode; Legacy mode returns an honest unavailable response. No fallback to retained booking tables occurs. Queue and record routes are lazy-loaded, preserve bookingId/rentalId through refresh/Back and recover missing links through the rental queue or My bookings.

RentalStartedEvent uses the booking Kafka topic, retains one stable outbox event ID across retries, and renders one Rental notification for the booking customer with relatedEntityId equal to rentalId. NotificationService deduplicates both delivery channels.

## Schema upgrade and release gate

Merging does not activate, migrate or deploy. Legacy/inactive defaults stay unchanged. DevOps must coordinate Fleet, Rental and Notification versions under a controlled write pause before enabling handovers.

For a fresh extraction, the existing guarded import now installs Rental schema 003_pickup and Fleet hold columns after both import receipts are verified. For an already active schema-2 Rental deployment, do not re-import the old export: it cannot overwrite later bookings. Use the new upgrade-pickup mode:

1. Pause booking/maintenance/vehicle/rental writes, close ingress, stop recovery and producer writers. Verify both ownership nonces and local database targets. Keep the prior frontend backups.
2. Preserve fresh auth, Fleet and Rental custom PostgreSQL dumps and uploads. Restore into different isolated verification databases; compare relationships, row fingerprints and SHA-256 hashes. Keep private dumps, exports and receipts outside Git. The receipt must include fleetflow_auth, fleetflow_fleet and fleetflow_rental, each restoredAndVerified=true, plus verified upload archive hashes.
3. Configure only explicit loopback DB_HOST and verified RENTAL_MIGRATION_SOURCE_DB / RENTAL_MIGRATION_TARGET_DB. The rehearsal CLI accepts the established database-name allowlists and never derives targets from deployment/Azure settings.

```powershell
dotnet artifacts/rental-migration/RentalMigration.dll --mode export-target --file rental-before-pickup.json
dotnet artifacts/rental-migration/RentalMigration.dll --mode upgrade-pickup --file rental-before-pickup.json --sha256 <export-sha256>
dotnet artifacts/rental-migration/RentalMigration.dll --mode upgrade-pickup --file rental-before-pickup.json --sha256 <export-sha256> --backup <fresh-verified-receipt.json> --write-pause confirmed --apply true
```

4. Upgrade verifies the unchanged Rental export and matching verified Fleet/Rental receipts. Fleet's additive hold columns are installed first, then Rental's own table and schema-3 marker under an advisory lock. A partial upgrade can be rerun; differing schema checksums/owners are refused. No existing booking, vehicle, maintenance or image rows are rewritten. Rental readiness requires schema 3, matching owned-service name and verified import nonce before opening ingress.
5. Package all four services and migration SQL, apply coordinated configuration, confirm readiness and API permissions, then reopen writes. Azure provisioning/production migration/deployment remain DevOps responsibilities; this local tool is not an Azure cutover command.

## Rollback limits

Before any pickup intents exist, retain the schema-3 additions and restore the coordinated prior service/frontend artifacts under a write pause, with fresh safety backups. Check for pending workflows and outboxes first. Do not drop the new tables/columns merely to undo UI code. Existing post-extraction booking reconciliation requirements still apply.

**After any pending or successful pickup, do not restore schema-2 Fleet/Rental executables, switch to Legacy, clear holds, delete pickup history, or restore old working-database dumps.** Those versions cannot represent active vehicle protection. The guarded legacy reconciliation command now explicitly refuses pickup history. Keep the compatible services and reconcile durable intents; a data-preserving lifecycle-aware rollback requires a separate reviewed migration. A frontend-only rollback cannot remove Fleet's active hold or the server's cancellation restriction.

Permitted local rehearsal cleanup targets are only the exact UUID-suffixed verification databases and recorded processes in its private review manifest. Verification restores never target working databases. Deployed Azure data is untouched.

## Design acceptance

Reviewed at 1440 × 900: staff queue, real sedan record/photo, pickup form, focus-contained confirmation, successful handover and record reload; customer own rental record and cancellation visibility. Controls remain stationary. The only new motion is a short background-color confirmation accent, immediately visible and disabled for reduced-motion users. Missing photos use a neutral Phosphor car icon; no marketing photo is substituted. Queue counts, estimates and references come from the APIs. Staff/customer names are not invented for cross-service IDs.

The confirmation adapts the researched [21st.dev shadcn alert-dialog pattern](https://21st.dev/%40shadcn/components/alert-dialog/with-icon) through the existing tested Modal, with a neutral initial action and submission guard. Summary cards reuse the attributed Magic UI Bento adaptation. Notices remain in public/credits.html; no new icon library or animation framework is installed.

Desktop screenshots are in docs/review/s4-pickup-*.jpg. Dedicated mobile/tablet work was skipped as requested. There is no automatic fee policy, return form or full timeline yet.

## Regression acceptance

- Preserved the 554-test backend and 132-test frontend baselines. Added 11 Rental tests, one notification test and eight frontend tests. The final full backend run passed 566/566 with no skips; focused Rental (33/33) and notification inbox (6/6) checks also passed.
- All 140 frontend tests passed; lint and production build passed. Entry plus recursively static imports is below 350,000 uncompressed bytes; exact measurements and independent lazy-route closures are in s4-01-verification.json.
- Isolated four-service API rehearsal migrated four bookings and sixteen notifications, confirmed one pickup through compatibility paths, rejected customer pickup (403), repeated pickup/cancellation (409), updated vehicle InUse/mileage and delivered exactly one pickup notification. A second handover passed through Browser. Retained legacy booking tables had zero new writes.
- PostgreSQL tests cover timing boundaries, invalid details, odometer progression, permissions/ownership, cancellation races, concurrent handovers, lost acknowledgements, original actor/timestamp retention, local event-write rollback, restart recovery, durable holds and guarded upgrade/legacy-rollback refusal.
- Working auth/Fleet data and five upload files passed fresh backup/isolated-restore hash verification. Dumps, credentials and private record manifests remain in the Z-drive cache outside Git.
