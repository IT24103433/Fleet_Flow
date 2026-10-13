# Rental extraction: bookings, availability and recovery

RentalService now owns booking creation, customer history, cancellation and booking-report data. FleetService owns vehicles, maintenance and the reservation ledger. NotificationService owns the inbox; each producer owns its own durable notification outbox. IdentityService owns user and service authentication. The four hosts build and run independently with separate owned databases.

**Safe defaults remain `RENTAL_OWNERSHIP=Legacy`, `RENTAL_SERVICE_ACTIVE=false`, and a blank `VITE_RENTAL_API_URL`.** Merging this PR does not migrate data, switch the active writer or deploy anything. The approved frontend stays in its existing layout. Pickup, return and lifecycle timelines are subsequent story PRs. Extraction is architecture work outside the 12-story/36-point Sprint 4 baseline; S4-07 is not counted again.

## Ownership and durability

Fleet's `VehicleReservations` ledger keeps one record per booking ID, including released tombstones. Reservation commands, maintenance and vehicle-status changes all acquire the same PostgreSQL vehicle-row lock. Overlapping intervals conflict; adjacent intervals can coexist. Unfinished reservations stay blocked until their workflow is resolved. Retrying the same command returns its original price and vehicle snapshot; a released ID cannot be reused. Remote maintenance and retirement consult this ledger rather than retained legacy booking rows. Existing conservative maintenance blocking remains; planned-window behaviour belongs to S4-04.

Rental first commits a `BookingWorkflows` intent. A worker or request then locks that workflow and calls Fleet with the same booking ID. Only a durable Fleet acknowledgement permits the local booking, workflow completion and notification event to commit together. A lost acknowledgement, API outage, process crash or local event-write failure leaves a persisted intent; recovery retries the idempotent command. It never reports booking success before both the availability block and booking are durable. Definitive reserve rejections retain a private rejected workflow without creating a booking.

Cancellation commits its intent before releasing Fleet's block. A lost release acknowledgement leaves the intent for recovery. Completion updates the booking and saves its event atomically. During this short recovery interval the last booking status can still be Confirmed even though the durable cancellation intent has released the block; the request returns unavailable rather than claiming success. No automatic expiry or compensation silently frees an uncertain reservation.

`Idempotency-Key` is an optional non-empty UUID, scoped to the customer and exact vehicle/date details. Concurrent identical retries return one booking and produce one event; changed details with the same key conflict. Timestamps are canonicalised to PostgreSQL microseconds before comparisons. The frontend retains an in-memory key after uncertain requests and clears it after success or definitive rejection. Reloading loses that key: customers should check My bookings before creating another request, as the existing error copy states.

Booking reads resolve live vehicle details through Fleet's restricted API. The stored snapshot preserves the original price/vehicle reference; it is not a substitute for unavailable current vehicle data. Ownership is checked in Rental's database before any external metadata call, so another customer's record stays 404 even during a Fleet outage. Fleet reporting combines Rental's own report/statistics API responses with current Fleet metadata; it never queries Rental tables.

Each Rental booking event uses the established leased outbox algorithm: durable retries, separate inbox/Kafka checkpoints, event-ID deduplication, exponential backoff, failed visibility and guarded administrator replay. With Kafka explicitly disabled for development, an inbox delivery can complete without a Kafka acknowledgement. Delivery UI counts refer to the selected producer and latest 100 rows. The existing licensed Bento/confirmation components, stationary controls, account presentation, fonts and theme are retained.

## Routes and contracts

| Host | Method/path | Access and result |
|---|---|---|
| Rental | POST `/api/bookings` | CUSTOMER; original booking fields, 201 after durable completion; 400/404/409 and unavailable 503 |
| Rental | GET `/api/bookings` | Current CUSTOMER's records, original ordering and fields |
| Rental | GET `/api/bookings/{id}` | Owning CUSTOMER, ADMIN or FLEET_MANAGER; absent/foreign customer record 404 |
| Rental | POST `/api/bookings/{id}/cancel` | Owning CUSTOMER; original status rules and response; repeat cancellation 409 |
| Rental | GET `/api/bookings/check-availability` | Anonymous; existing query/result shape, overlap 409; advisory until reservation commit |
| Rental | GET `/internal/reports/bookings` and `/statistics` | `rental.reports.read`, granted to Fleet only |
| Rental | GET `/api/notifications/outbox` and POST `/{id}/replay` | ADMIN; this producer's private outbox metadata/replay, no payloads |
| Fleet | PUT `/internal/reservations/{bookingId}` | `fleet.reservations.write`; stable command ID and acknowledgement |
| Fleet | POST `/internal/reservations/{bookingId}/release` | Same restricted scope; idempotent released tombstone |
| Fleet | GET `/internal/reservations/availability` | Rental's restricted Fleet read/reservation scopes |
| Fleet | GET `/internal/vehicles/{id}` | `fleet.read`; live vehicle response |

Fleet preserves the existing public booking paths by forwarding authenticated user requests in explicit Remote mode. Roles, ownership, forced-password requirements, fields, errors and Location context remain intact. A failed forward returns 503; there is **no stale-table fallback**. Legacy booking services and both synchronous/asynchronous EF write paths are guarded when Remote ownership is selected. Browser tokens cannot call internal commands; service tokens cannot use browser endpoints. Destinations are fixed configuration, redirects are disabled, and non-development internal connections require HTTPS.

All four APIs expose OpenAPI and liveness/readiness endpoints. Rental's business routes and background writers require a matching verified migration receipt and schema version two. Fleet's Remote commands and availability-changing writes require the matching ledger receipt. An unverified/inactive service returns unavailable after authorization. Database initialization remains an explicit offline operation for Rental.

## Local configuration and cutover

Keep Identity 5001, Fleet 5002, Rental 5003 and Notification 5004. `tools/Start-Services.ps1` reads local `.env` only into its process. The opt-in Compose overlay supplies internal hostnames and publishes each independent artifact; applying that overlay is a separate action. CI builds/tests all seven backend test projects and publishes four APIs plus both migration tools.

Before cutover configure private internal keys/client secrets, Notification's verified import/activation, and these explicit Rental settings:

```text
RENTAL_OWNERSHIP=Legacy
RENTAL_SERVICE_ACTIVE=false
RENTAL_MIGRATION_ID=
INTERNAL_RENTAL_URL=http://127.0.0.1:5003/
INTERNAL_FLEET_URL=http://127.0.0.1:5002/
INTERNAL_NOTIFICATION_URL=http://127.0.0.1:5004/
VITE_RENTAL_API_URL=
```

The offline `RentalMigration` console tool accepts only exact owned/legacy database names or isolated verification prefixes on loopback connections. It reads `DB_*`, optional `RENTAL_MIGRATION_SOURCE_DB` and `RENTAL_MIGRATION_TARGET_DB`; it does not derive destinations from Azure settings or user requests. This is a local rehearsal tool, not an Azure production cutover command. DevOps owns secure production provisioning, migration and deployment.

1. Pause all booking, maintenance and vehicle writes; stop recovery/producer writers when taking the migration snapshot. Keep ingress closed during activation. Preserve auth/Fleet PostgreSQL custom dumps, uploads and an affected-record manifest. Verify each dump by restoration into a different isolated database, compare row fingerprints/relationships and SHA-256 hashes, and retain a private backup receipt.
2. Publish the migration artifact. Export a new file; exports refuse to overwrite an existing file. Validate/dry-run the hash before allowing writes:

```powershell
dotnet tools/RentalMigration/bin/Release/net10.0/RentalMigration.dll --mode export --file booking-export.json
dotnet tools/RentalMigration/bin/Release/net10.0/RentalMigration.dll --mode import --file booking-export.json --sha256 <export-sha256>
dotnet tools/RentalMigration/bin/Release/net10.0/RentalMigration.dll --mode import --file booking-export.json --sha256 <export-sha256> --backup <verified-backup-receipt.json> --write-pause confirmed --apply true
```

3. Import preserves all booking IDs, customer/vehicle references, statuses, amounts and timestamps, plus deterministic snapshots of referenced vehicle/image metadata. It verifies the source again, rejects overlapping active bookings, and refuses different or later target data. Rental's import receipt remains unverified until Fleet's matching ledger commits. A failed leg can resume with the identical export; repeated import never overwrites business history. The two databases are deliberately not claimed to share an atomic transaction.
4. Retain legacy tables unchanged for recovery. Put the returned `MigrationId` into both Fleet and Rental configurations, select Fleet Remote ownership and activate Rental together under the write pause. Check both readiness endpoints and the verified record/ledger counts before opening ingress. Notification must already be active/verified. There is one active booking writer.
5. The frontend may keep Fleet compatibility paths, or explicitly set `VITE_RENTAL_API_URL` to the trusted Rental base URL after cutover. Configuring that URL also enables Fleet/Rental producer selection in the administrator delivery page. Images continue to load from Fleet; no runtime path refers to the Z drive.

The backup receipt format matches the earlier Notification tool: `verified=true`; `databases.fleetflow_auth` and `.fleetflow_fleet` contain `dump`, `sha256`, and `restoredAndVerified=true`; `uploads` contains `archive`, `sha256`, and `verified=true`. Reconciliation additionally requires a fresh `.fleetflow_rental` dump. The CLI verifies archive bytes and refuses missing/unverified receipts or an absent `--write-pause confirmed` acknowledgement. Keep receipts/dumps/exports outside Git: they contain private account/history data.

## Rollback after cutover

Before any new Rental writes, a coordinated switch back to the preserved Legacy writer is possible under a write pause. **After new writes, changing only the URL/mode would lose or misrepresent history.** Keep ingress closed, stop all producers/recovery, take and restore-verify fresh auth/Fleet/Rental backups and uploads, resolve every pending recovery workflow and outbox delivery, then export Rental's current records and reconcile:

```powershell
dotnet tools/RentalMigration/bin/Release/net10.0/RentalMigration.dll --mode export-target --file rental-current.json
dotnet tools/RentalMigration/bin/Release/net10.0/RentalMigration.dll --mode reconcile --file rental-current.json --sha256 <export-sha256> --backup <fresh-verified-receipt.json> --write-pause confirmed --apply true
```

The reconciler verifies the paused target export and Fleet ledger, preserves immutable historical fields, permits cancellation/completion progression, adds new booking IDs without deleting history, and rolls back on conflicts. It refuses pending workflows or undelivered events. It does not roll Notification ownership back or reset inbox/read states. Only after verifying reconciled counts/statuses may DevOps disable Rental, select Fleet Legacy and reopen ingress. Retain the ledger/new database/backups. A later return to Remote requires another coordinated migration/reconciliation plan; the old verified import must not be blindly reused after Legacy resumes writes.

## Acceptance and remaining work

See [verification evidence](rental-extraction-verification.json) and `docs/review/s4-rental-*.jpg`. Local backend verification preserves the 532-test prerequisite baseline, adds 22 focused Rental checks, and covers real PostgreSQL conflicts/recovery plus existing real-Kafka tests. Frontend retains all 129 prerequisite tests and adds three retry/destination checks. Desktop review is 1440x900 only; mobile/tablet work was not performed. No new animation framework, design replacement, lifecycle feature, deployment or working-data cutover was added.

The rehearsal copied four historical bookings and 16 notifications into isolated clones, ran all four APIs, checked direct/compatibility booking and cancellation, idempotency, conflicts, account isolation, report composition and durable inbox delivery. A browser-created two-day booking returned Confirmed and LKR 18,000, appeared in the customer's account, cancelled correctly, and produced its events. Guarded rollback reconciled six records (four original plus two new) with fresh restore-verified backups. Original databases and all five uploads remained byte/fingerprint-equivalent; the rehearsal processes and databases were removed.

Pickup (S4-01), return (S4-02), timeline (S4-03), maintenance stories, access revocation, API hardening, exports and final release evidence follow in their own PRs. Cross-service live account-version validation remains S4-08; extraction retains the existing authentication contract. S4-12 stays open until DevOps deploys and QA verifies the deployed release.
