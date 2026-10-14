# Notification extraction and S4-07: durable delivery

This PR implements NotificationService's inbox ownership and reliable notification delivery. **Defaults remain `NOTIFICATION_OWNERSHIP=Legacy` and `NOTIFICATION_SERVICE_ACTIVE=false`.** Merging or starting the new host does not cut over a writer or migrate working data. The local website and Azure deployment remain unchanged.

## Runtime boundaries

- Fleet owns the booking/maintenance transaction and its `NotificationOutbox` rows. Booking creation, cancellation, maintenance scheduling and maintenance status changes allocate an event ID once and save that event with the business change in the same `SaveChanges`/vehicle-lock transaction. An outbox failure rolls the business write back. The post-commit channel only wakes a polling worker; losing a wake-up does not lose an event.
- NotificationService owns `fleetflow_notifications`: inbox rows, transactional event receipts, rejected Kafka records and migration receipts. It never queries Fleet or Identity tables. Original booking, vehicle and account IDs remain references rather than cross-service foreign keys.
- Legacy mode keeps the inbox and existing consumer inside Fleet. Explicit Remote mode stops Fleet's notification consumer, forbids its local inbox reads/writes and forwards existing `/api/notifications` paths to the configured Notification API. Failed forwarding returns unavailable; it never reads a stale legacy table.
- In Remote mode Fleet sends the saved event to `POST /internal/notifications/events` with a short-lived `notifications.deliver` service token. Rental receives the same limited grant for its subsequent extraction. Browser tokens cannot deliver internal events. The returned event ID must match before the outbox checkpoints its inbox leg.
- The internal token request DTO now validates constructor parameters correctly in ASP.NET MVC. A real HTTP regression test covers successful issuance, invalid credentials and overlong/missing fields; direct controller tests alone had missed this foundation bug.

Maintenance remains in Fleet. Booking APIs, ownership/status rules, response shapes, pricing and cancellation behaviour are unchanged. Rental extraction, pickup/return and other Sprint 4 stories are separate PRs.

## Delivery and recovery

The PostgreSQL worker claims one due row using an atomic `UPDATE ... FOR UPDATE SKIP LOCKED`, a unique lease token and a 60-second lease. Another process cannot claim that row until the lease expires. Checkpoints compare the lease token so a stale worker cannot overwrite a newer claim. A process restart can resume an expired lease.

Inbox persistence and Kafka publication are separately checkpointed. With Kafka enabled, publication uses an idempotent producer, all-replica acknowledgement and an explicit persisted-delivery check. Broker/HTTP failures remain durable retries rather than being swallowed by the old best-effort producer. Retries back off exponentially from two seconds, capped at 300 seconds; a reported failure after eight attempts becomes `Failed`. A crash before a failure checkpoint leaves the lease recoverable. Invalid saved payloads fail immediately.

With Kafka explicitly disabled for local development, inbox delivery can complete without broker publication. `KafkaPublishedAt` remains null; the UI does not claim Kafka acknowledged it. Enabling Kafka later does not retroactively republish previously delivered local-only rows.

NotificationService validates all recipients before writing. Its receipt and every inbox recipient commit in one transaction. Concurrent internal delivery and Kafka replay share the same event-ID boundary. Identical duplicates return success without changing IDs, timestamps or read states; reusing an event ID with different content returns 409. Historical imported rows without original payloads are matched against their rendered content and retained unchanged on replay.

The consumer requires pre-provisioned domain topics (`AllowAutoCreateTopics=false`) and commits Kafka offsets only after durable persistence. Invalid payloads, wrong topics and content collisions are recorded once by topic/partition/offset before their offset is committed. Database failures seek back and retry. Both consumer startup and message processing require the configured migration receipt to pass readiness, so an unverified activation cannot write through Kafka. Rejected payloads are retained privately, limited to 16 KB, with a SHA-256 and generic error code; administrator responses exclude their contents.

## API and UI contracts

NotificationService retains:

| Method/path | Access | Result |
|---|---|---|
| GET `/api/notifications` | Current authenticated account | Existing notification array and fields |
| GET `/api/notifications/unread-count` | Current account | `{count}` |
| PATCH `/api/notifications/{id}/read` | Owning account | 204, or 404 for absent/other-owner records |
| POST `/api/notifications/system` | Administrator | Existing validated request, 201 |
| GET `/api/notifications/delivery-failures` | Administrator | Latest 100 rejected Kafka offsets, metadata only |
| POST `/internal/notifications/events` | `notifications.deliver` service scope | `{eventId,processed}` after commit; duplicate `processed=false` |

Inactive/unverified business requests return 503 after authentication/permissions checks. Forced-password users remain blocked. Health/OpenAPI stay available for startup diagnosis. Schema-two readiness checks the Notification owner and the configured verified migration ID.

Fleet adds administrator-only GET `/api/notifications/outbox?status=Pending|Retry|Failed|Delivered` and POST `/api/notifications/outbox/{id}/replay`. The list exposes no payload or recipient details. Replay accepts only failed, unleased records and preserves their event ID and delivery checkpoints; it records the actor, time and replay count. A conflicting replay returns 409; missing IDs return 404.

The lazy `#/admin-notification-delivery` page sits in Administration. It uses the existing graphite/ivory/violet workspace, fonts, named Phosphor icons, licensed Bento panels and a replay confirmation adapted from the [21st.dev shadcn alert-dialog pattern](https://21st.dev/%40shadcn/components/alert-dialog/with-icon). The existing modal retains focus containment/restoration. Tables, filters and actions remain stationary. Replay has an immediate submission guard, and its success copy says **queued for another attempt**, not delivered. Current-view counts cover at most the latest 100 records; they are not invented global metrics. Existing entry-point navigation, accounts and marketing are preserved.

The inbox client supports `VITE_NOTIFICATION_API_URL`; leaving it blank retains Fleet compatibility routing. The outbox client continues to use Fleet, which owns that producer ledger. No fallback request is sent to legacy after a configured Notification URL fails.

## Local migration and write pause

Only the **offline tool** may read the legacy source database. Runtime APIs never read another service's tables. `NotificationMigration` is separately published by CI. The CLI defaults to a dry run for import/reconciliation and never drops databases or deletes history.

1. Keep the write owner on Legacy. Back up `fleetflow_auth`, `fleetflow_fleet` and uploads, hash them, and restore the dumps into isolated verification databases with constraints enabled. Preserve an affected-record manifest including notification IDs/event IDs, timestamps, read states, account/vehicle/booking references. Do not reset Docker volumes or overwrite working databases while verifying.
2. Start a controlled write pause covering booking/maintenance mutations, notification creation/read-state changes and both consumers. Drain or retain pending outbox work; check consumer offsets and stop the legacy notification consumer before starting the new owner. Export **after** the pause so no newer rows or read changes can be omitted.
3. Load the verified local `DB_*` credentials into process environment. Use explicit loopback (`127.0.0.1`) for local rehearsal and select a new export path:

```powershell
dotnet run --project tools/NotificationMigration -- --mode export --file <new-export.json>
```

The tool prints the export SHA-256. It preserves original row fields and sorts IDs canonically. Import verifies supplied SHA-256, uniqueness, non-empty identifiers, UTC timestamps and field limits before any write:

```powershell
dotnet run --project tools/NotificationMigration -- --mode import --file <export.json> --sha256 <export-hash>
dotnet run --project tools/NotificationMigration -- --mode import --file <export.json> --sha256 <export-hash> --backup <verified-receipt.json> --write-pause confirmed --apply true
```

`--apply true` requires confirmed pause plus verified dump/upload hashes. The tool initializes the new owned database's foundation and schema two, validates schema ownership/checksum, imports transactionally and compares every row before recording a verified migration ID. A repeated matching import performs no writes. Different target rows or subsequent new-service writes cause refusal, never overwrite. Failed verification rolls back the transaction.

The receipt format contains `verified=true`, per-database `dump`, `sha256`, `restoredAndVerified`, and an `uploads` archive/SHA-256/verified block. Keep these private; the repository's evidence includes only hashes/counts. The direct library methods are for the controlled test harness; operator writes must use the guarded CLI.

Permitted destinations: source `fleetflow_fleet` (or explicitly disposable `fleetflow_hardening_*`), target `fleetflow_notifications` (or `fleetflow_notifications_foundation_test_*`), on loopback only. `NOTIFICATION_MIGRATION_SOURCE_DB` and `NOTIFICATION_MIGRATION_TARGET_DB` exist for isolated rehearsal; neither can select auth or an arbitrary remote database. Default names select the intended working domains, so do not run `--apply` casually. No Azure endpoint is derived from deployment settings. Production migration needs a separately authorized DevOps procedure, retaining these validation/pause checks.

4. Keep writes paused; configure the returned ID and independent service secrets:

```text
SERVICE_AUTH_ENABLED=true
NOTIFICATION_MIGRATION_ID=<verified-import-id>
NOTIFICATION_SERVICE_ACTIVE=true
NOTIFICATION_OWNERSHIP=Remote
INTERNAL_IDENTITY_URL=http://127.0.0.1:5001/
INTERNAL_NOTIFICATION_URL=http://127.0.0.1:5004/
```

Use HTTPS for deployed internal endpoints. Start Notification and Fleet with these settings, check readiness 200, then verify account ownership, direct/compatibility inbox equality, read states and delivery before resuming writes. Update frontend `VITE_NOTIFICATION_API_URL` only after the new owner is ready. There is exactly one active inbox writer; never leave the old Fleet consumer running against the new one. The old table remains unchanged for recovery, and pending outbox event identities must be retained.

## Rollback after new writes

Do not simply flip routing back to the stale legacy table. Pause all affected writes and consumers again. Take fresh, verified dumps of **auth, fleet and notifications**, plus uploads and an outbox/offset manifest. Export the new inbox while the new API/consumer is stopped or under the confirmed pause:

```powershell
dotnet run --project tools/NotificationMigration -- --mode export-target --file <new-target-export.json>
dotnet run --project tools/NotificationMigration -- --mode reconcile --file <target-export.json> --sha256 <hash>
dotnet run --project tools/NotificationMigration -- --mode reconcile --file <target-export.json> --sha256 <hash> --backup <fresh-three-database-receipt.json> --write-pause confirmed --apply true
```

Reconciliation is restricted to the verified loopback legacy Fleet database. It inserts new rows, checks all immutable fields of existing IDs, and merges `IsRead` with logical OR so acknowledgements cannot be lost. It never deletes history, reassigns IDs or silently overwrites conflicting content. Any collision rolls back the whole reconciliation. Check merged counts/identifiers and retained outbox checkpoints; only then set Notification inactive, stop its consumer, restore frontend compatibility routing and restart Fleet in Legacy mode under the pause. Do not run both writers. Keep the new database, receipts, dumps and uploads until recovery is accepted.

Before any new-service writes, disabling the inactive host and reverting the PR leaves legacy business data intact; retain the additive outbox table rather than deleting committed work. After writes, use reconciliation before any code/config rollback.

## Verification and delivery

See [verification manifest](notification-extraction-verification.json) and [desktop workspace](review/s4-notification-delivery-1440.jpg), [confirmation](review/s4-notification-replay-1440.jpg), [delivered replay](review/s4-notification-delivered-1440.jpg), [unavailable service](review/s4-notification-unavailable-1440.jpg).

The local milestone full run passed 530 backend tests; final focused Kafka-activation and real-broker replay tests add two unique cases (532 total), with 129 frontend tests, lint and production build passing. This preserves the 502-backend/126-frontend baseline. Coverage includes transactional business rollback, concurrent leases/delivery, restart recovery, broker-outage retry without resetting read states, HTTP broker model binding, inbox permissions/forced password, import checksum/identity/read-state preservation, idempotence, rollback reconciliation and administrator replay guards. Three nullable warnings remain in unchanged legacy Identity tests.

Real Kafka verification uses unique temporary domain topics and an isolated owned database, covering duplicate events, consumer restart/read-state preservation and durable rejected payloads. CI supplies isolated PostgreSQL and Kafka containers and runs that check as well.

A real isolated cutover rehearsal restored verified database copies and imported all 16 historical notifications. New booking/cancellation events passed through real Identity internal tokens to NotificationService, compatibility responses matched direct inboxes, cross-account writes returned 404, and desktop replay preserved the original notification ID/read state. A post-inbox failure was injected on a real cancellation outbox record **only in the disposable clone** to review replay. Original working databases/uploads were not changed; all rehearsal processes and databases are removed afterward. No Azure deployment or production cutover was performed.

Design acceptance was limited to 1440×900: populated/empty/failure states, filtering, replay confirmation, visible header logout and no page-wide horizontal overflow. No mobile/tablet optimization or review was performed. Entry JavaScript uses the existing uncompressed entry-plus-static-import definition; lazy homepage and delivery-route closures are reported separately in the manifest. No new animation framework or paid generation service was introduced.

S4-07 is counted once. DevOps controls deployment and production cutover; QA verifies the deployed release. Rental extraction is the next architecture PR after human merge. S4-12 remains open until actual deployment verification.
