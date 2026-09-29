# Sprint 3 notification management

## Architecture and scope

Notifications live in FleetService, using its PostgreSQL database, JWT authentication and Kafka configuration. IdentityService remains the source of user identities and roles. No service, booking cancellation endpoint or maintenance work-order model is added.

BookingService commits the booking before calling `INotificationEventDispatcher.TryEnqueue`. The singleton background dispatcher owns a bounded queue of 1,000 events. It creates a fresh scoped context, persists notifications, and then publishes through the existing Kafka producer. Local notification delivery also runs when Kafka is disabled or unavailable. Database failures receive three local attempts with fresh scopes; Kafka failures are logged by the existing producer. Neither is awaited by booking requests. Queue saturation is logged; the business result stays successful.

This is best-effort delivery, consistent with the existing infrastructure. The queue is not a durable outbox: process termination can lose queued events, prolonged database outages can exhaust local retries, and failed Kafka publications are not retried durably. There is no claim of guaranteed delivery. If durable delivery is required during System Integration, add an outbox/reconciliation mechanism with explicit ownership of its failure semantics.

Notification consumers use a separate consumer group and manual offset commits. They persist all recipients before committing. On database failure they seek back to the failed offset and retry; recipient-specific deduplication permits safe recovery after partial fan-out. Malformed/unsupported events are logged and skipped. There is no dead-letter topic in this sprint.

`Notifications` contains ID, event ID, target user ID, category, title, message, related entity ID, vehicle ID, UTC creation timestamp and read state. A unique `(EventId, TargetUserId)` index protects against replay and concurrent local/Kafka processing. Duplicate handling never resets read state. The target user ID is an external IdentityService reference, not a cross-database foreign key. Related references are identifiers, so cancellation/work-order persistence remains independent.

The schema follows existing `EnsureCreated` plus idempotent PostgreSQL table initialization. Startup also creates the new table and indexes for pre-existing databases. Existing IdentityService user and FleetService vehicle producers/consumers are unchanged.

## API and frontend

All endpoints require a valid JWT. Ownership uses `ClaimTypes.NameIdentifier` or mapped `sub`; a missing/invalid user ID returns 401. No inbox endpoint accepts a target-user override. Administrators also only read/update their own inbox.

| Method | Endpoint | Response |
| --- | --- | --- |
| GET | `/api/notifications` | Array of current user's notifications, newest first; empty array for empty inbox |
| GET | `/api/notifications/unread-count` | `{ "count": 0 }` |
| PATCH | `/api/notifications/{id}/read` | 204; 404 for missing or another user's notification |
| POST | `/api/notifications/system` | ADMIN only; 201 after persistence |

System request: `{ "targetUserId": "<identity-user-guid>", "title": "...", "message": "..." }`. Title is limited to 200 characters, message to 2,000; neither may be blank. Trusted administrators must supply an existing IdentityService user ID; FleetService does not synchronously query IdentityService to validate user existence.

Inbox response fields: `id`, `category`, `title`, `message`, `relatedEntityId`, `vehicleId`, `createdAt`, `isRead`. Target user and event ID are not exposed.

The customer hash route is `#/notifications`; staff use `#/staff-notifications`. Both use the same inbox page in the existing shells. Navigation shows the unread count. The page supports loading, empty and error states, dates, read/unread styling and marking individual notifications read. The inbox and badge refresh every 30 seconds; manual refresh is available. No email, SMS, push, preferences or templates are introduced.

## Kafka contracts and later integration

Configuration extends the existing `Kafka` section. `Kafka:Enabled` still controls Kafka only; local delivery does not require a broker.

| Setting | Default |
| --- | --- |
| `BookingEventsTopic` | `fleetflow.booking.events` |
| `MaintenanceEventsTopic` | `fleetflow.maintenance.events` |
| `NotificationConsumerGroupId` | `fleetflow.notifications.group` |

Payloads use camelCase properties and `eventType` equal to the C# contract name. Each event requires a non-empty, stable `eventId` and a UTC `occurredAt`. The producer generates the event ID once and preserves it across retries. All entity/user IDs must be non-empty GUIDs. Publication key is the event ID; consumers must not depend on cross-event ordering. Configure Kafka topic ACLs so only trusted service producers can write these events.

| Contract | Topic | Fields beyond event ID/type/time | Integration status |
| --- | --- | --- | --- |
| `BookingCreatedEvent` | booking | `bookingId`, `customerId`, `vehicleId` | Existing successful creation wired after persistence |
| `BookingCancelledEvent` | booking | `bookingId`, `customerId`, `vehicleId` | Consumer ready; PR #25 cancellation producer required |
| `MaintenanceScheduledEvent` | maintenance | `maintenanceId`, `vehicleId`, `activity`, `targetUserIds` | Consumer ready; PR #26 work-order producer required |
| `MaintenanceStatusChangedEvent` | maintenance | above plus `status` | Consumer ready; PR #26 status-change producer required |

For PR #25, enqueue `BookingCancelledEvent` only after successful cancellation persistence; obtain `customerId` from the persisted booking, not the cancelling staff actor. Reuse the post-commit dispatcher and keep notification failures outside the cancellation transaction. This sprint does not implement cancellation.

For PR #26, enqueue the maintenance contracts only after the work-order transaction succeeds. Resolve assigned/authorized maintenance recipient IDs in the maintenance owner using trusted IdentityService role/assignment data; never copy recipient IDs from an untrusted browser request. `activity` is a nonblank description up to 200 characters; `status` is nonblank up to 50 characters. `targetUserIds` must contain 1–1,000 valid user IDs, with duplicates collapsed. Consumers validate the entire recipient list before persistence. A completed work order can use `MaintenanceStatusChangedEvent` with the owner's completed status; no extra completed-event contract is needed.

Services in this process can call `TryEnqueue` for local persistence plus Kafka publication. External domain producers can use the existing `IKafkaProducerService.PublishAsync` with these contracts and topics after their transactions commit; they must arrange background/non-blocking dispatch. Maintain stable IDs for redelivery.

Example cancellation payload:

```json
{
  "eventType": "BookingCancelledEvent",
  "eventId": "52e6cfd0-e621-4c78-8e4b-060c5b8c34a7",
  "occurredAt": "2026-09-29T06:00:00Z",
  "bookingId": "d3d11711-b989-4b31-a414-d63223490ba6",
  "customerId": "bc903c0d-47fc-45c3-b35b-7dc3497d437c",
  "vehicleId": "e092157a-dd79-4fdf-b5f4-c1611351f7e2"
}
```

## Validation coverage

Backend tests cover persistence across contexts, ownership, empty inboxes, read state/counts, JWT HTTP authorization, system creation restrictions, booking-to-background-inbox delivery, booking survival on queue saturation/dispatch failure, Kafka producer failure isolation, all four JSON contract round trips and event-to-notification mapping, per-recipient replay idempotency, invalid payloads/identifiers and the unique model index. They use the repository's EF InMemory and WebApplicationFactory patterns. Real PostgreSQL unique-violation races and broker offset/rebalance behavior still require environment-backed System Integration testing.

Frontend tests execute the notification API client with stubbed HTTP responses: contract fields, empty array/count, authenticated requests, mark-read 204, 401/404/503 handling, cancellation signals and session changes. UI production compilation is checked by the Vite build; interactive browser acceptance is a separate integration check.

No commits or pushes are part of this work.

## Validation results (2026-09-29)

Branch `feat/notification-management` starts from refreshed `origin/develop`, commit `0f194633813ad053436e9e416bc8ef7dc65c2876`. No PR #25/#26 changes were merged or cherry-picked.

| Check | Result |
| --- | --- |
| Focused notification backend tests | 27 passed, 0 failed |
| Root `FleetFlow.sln`: Identity / Vehicle tests | 101 / 68 passed (169 total) |
| `src/backend/FleetFlow.sln`: Identity / Fleet tests | 44 / 110 passed (154 total) |
| Full backend total | 323 passed, 0 failed, 0 skipped |
| Frontend tests | 57 passed, 0 failed (11 new notification tests) |
| Combined full-suite total | 380 passed; focused rerun excluded from this total |
| Backend compilation | Both solutions compiled successfully during their test runs; existing nullable warnings remain in root Identity tests |
| Frontend production build | Passed: Vite, 70 modules, JS 494.11 kB / gzip 121.10 kB |
| Frontend full lint | Failed with 2 existing `react-hooks/set-state-in-effect` errors in unchanged `BookingModal.jsx:27` and `MaintenanceDashboardPage.jsx:57` |
| Focused lint of all changed/new frontend JS/JSX | Passed |
| `git diff --check` | Passed |

The initial restricted HTTP test attempt encountered Windows Event Log access-denied exceptions. The focused suite and full backend service solution were rerun outside that restriction and passed. Test result files are kept under ignored `TestResults/notification-management/`.

### File inventory

Modified files:

- `src/backend/services/FleetService/FleetService.Api/Data/FleetDbContext.cs`
- `src/backend/services/FleetService/FleetService.Api/Messaging/KafkaSettings.cs`
- `src/backend/services/FleetService/FleetService.Api/Program.cs`
- `src/backend/services/FleetService/FleetService.Api/Services/BookingService.cs`
- `src/backend/services/FleetService/FleetService.Api/appsettings.json`
- `src/frontend/src/App.jsx`
- `src/frontend/src/components/layout/CustomerNav.jsx`
- `src/frontend/src/components/layout/StaffSidebar.jsx`

Added files:

- `src/backend/services/FleetService/FleetService.Api/Controllers/NotificationsController.cs`
- `src/backend/services/FleetService/FleetService.Api/Data/NotificationSchema.cs`
- `src/backend/services/FleetService/FleetService.Api/Dtos/NotificationDtos.cs`
- `src/backend/services/FleetService/FleetService.Api/Entities/Notification.cs`
- `src/backend/services/FleetService/FleetService.Api/Messaging/Events/NotificationDomainEvent.cs`
- `src/backend/services/FleetService/FleetService.Api/Messaging/Events/BookingCreatedEvent.cs`
- `src/backend/services/FleetService/FleetService.Api/Messaging/Events/BookingCancelledEvent.cs`
- `src/backend/services/FleetService/FleetService.Api/Messaging/Events/MaintenanceScheduledEvent.cs`
- `src/backend/services/FleetService/FleetService.Api/Messaging/Events/MaintenanceStatusChangedEvent.cs`
- `src/backend/services/FleetService/FleetService.Api/Messaging/NotificationEventProcessor.cs`
- `src/backend/services/FleetService/FleetService.Api/Messaging/NotificationEventDispatcher.cs`
- `src/backend/services/FleetService/FleetService.Api/Messaging/NotificationEventConsumerService.cs`
- `src/backend/services/FleetService/FleetService.Api/Services/NotificationService.cs`
- `src/backend/services/FleetService/FleetService.Tests/BookingNotificationDispatchTests.cs`
- `src/backend/services/FleetService/FleetService.Tests/NotificationControllerTests.cs`
- `src/backend/services/FleetService/FleetService.Tests/NotificationTests.cs`
- `src/frontend/src/components/NotificationNavButton.jsx`
- `src/frontend/src/pages/NotificationsPage.jsx`
- `src/frontend/src/pages/NotificationsPage.css`
- `src/frontend/src/services/notificationService.js`
- `src/frontend/src/tests/notificationService.test.js`
- `docs/notification-management.md`

Recommended commit structure (not executed):

1. Notification persistence, authenticated APIs and backend ownership/read-state tests.
2. Domain event contracts, background dispatch, Kafka consumer, booking hook and event/failure tests.
3. Customer/staff notification UI, frontend API tests and integration/validation documentation.

Remaining integration checks: connect PR #25 cancellation publication and PR #26 work-order publication/recipient resolution; run a real PostgreSQL/Kafka scenario (including unique-key races and consumer replay); manually verify the inbox UI with customer/staff sessions. Delivery durability and the two existing frontend lint errors remain explicit follow-up items.
