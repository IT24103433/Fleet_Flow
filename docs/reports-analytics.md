# Sprint 3 Reports & Analytics

## Architecture and branch

Branch: `feat/reports-analytics`, based directly on refreshed `origin/develop` at `0f194633813ad053436e9e416bc8ef7dc65c2876`. PRs #25, #26 and #27 were not merged, cherry-picked or used as code dependencies. The notification branch remains preserved separately.

Reports are read-only FleetService queries over the existing PostgreSQL-backed `FleetDbContext`. `ReportingService` uses `AsNoTracking`, SQL-compatible grouping, counts, sums and projections. It never calls booking/vehicle mutation services, changes persisted state, publishes Kafka events, or depends on Kafka consumers. No reporting database, new microservice, schema, migration, work-order model, cancellation implementation or charting dependency is introduced. Existing IdentityService, booking, fleet, maintenance dashboard and Kafka paths remain intact.

`TimeProvider` supplies an injectable UTC clock so utilization tests can check exact booking boundaries. Responses contain a UTC `generatedAt` retrieval timestamp. Each request queries persisted data afresh; HTTP responses and frontend requests disable caching. The UI refresh action reloads the report. Multiple aggregation queries use ordinary database reads rather than a historical reporting snapshot.

## APIs and permissions

All endpoints are GET-only and require authenticated JWT access. Role claims follow the existing IdentityService/FleetService conventions.

| Endpoint | Allowed roles | Response |
| --- | --- | --- |
| `/api/reports/fleet-summary` | ADMIN | Vehicle status totals and current booking utilization |
| `/api/reports/bookings` | ADMIN | Paginated booking projection with recorded status, dates, IDs, vehicle details and cost |
| `/api/reports/maintenance` | ADMIN, FLEET_MANAGER, MAINTENANCE_STAFF | Current vehicle maintenance rows plus maintenance activity availability/projection |
| `/api/reports/operational-statistics` | ADMIN | Fleet summary, booking statistics and maintenance statistics |

Anonymous requests return 401. Authenticated users outside the endpoint's allowed roles receive 403. Invalid booking filters return 400. Database connectivity/timeouts return a generic 503 response; connection details are not disclosed and failures are not replaced with fabricated zero metrics.

Booking query parameters: `page` (default 1, minimum 1), `pageSize` (default 25, 1–100), and optional `status` (`Pending`, `Confirmed`, `Cancelled`, `Completed`). Overflowing pagination offsets and undefined enum values are rejected. Rows sort by `createdAt` descending, then booking ID, for stable page ordering. The result includes `items`, `totalCount`, `page`, `pageSize`, `totalPages` and `generatedAt`. Empty results return an empty array and measured zero counts.

Booking rows contain `bookingId`, `customerId`, `vehicleId`, `licensePlate`, `make`, `model`, `status`, `startDateTime`, `endDateTime`, `totalCost`, `createdAt`, `updatedAt`. These are administrator-only. Customer profile names, email addresses and other personal fields are not fetched from IdentityService or included in the report.

## Metric definitions

All numbers come from persisted records; UI pages contain no demo values or fallback numeric metrics.

- Vehicle total and `Available`, `InUse`, `Maintenance`, `Retired` counts derive from `Vehicles.Status`.
- Utilization-eligible vehicles are all non-retired vehicles, including vehicles whose persisted status is maintenance.
- Currently booked vehicles are distinct eligible vehicle IDs with a `Confirmed` booking satisfying `StartDateTime <= generatedAt` and `EndDateTime > generatedAt`. Duplicate/overlapping bookings on one vehicle do not inflate the count. Pending, cancelled, completed and future bookings are excluded.
- Current booking utilization is `currentlyBookedVehicles / utilizationEligibleVehicles * 100`, rounded to two decimals. An empty eligible fleet returns `null` (displayed as unavailable); a nonempty fleet with no current confirmed bookings returns a measured zero. This is current booking coverage, not lifetime utilization or a vehicle-status-derived occupancy estimate.
- Booking totals and each status count derive from persisted `Bookings.Status`. Active means pending plus confirmed. Stored status is not inferred from dates and is never rewritten by reporting.
- Non-cancelled booking value sums stored `TotalCost` for pending, confirmed and completed bookings. The UI uses the application's existing LKR formatter. This is booking value, not payment collection, realized revenue, profit or refunded value.
- Vehicles in maintenance derive from persisted vehicle status. They are not counted as work orders.
- Work-order count, costed work-order count and recorded maintenance cost total are `null` while activity data is unavailable. When connected, recorded cost total sums only records with an actual cost, and the accompanying costed count makes partial cost coverage explicit. Available-but-empty records produce measured zero counts/costs.

## Maintenance integration boundary

The current base has no persisted maintenance work-order/history/cost entities. The maintenance report therefore provides two separate parts:

1. `currentVehicles`: actual vehicles whose status is `Maintenance`, including vehicle ID, plate, make/model, hub, mileage, status and nullable `lastVehicleUpdateAt`. A vehicle update timestamp is not treated as a maintenance start timestamp or an activity history entry.
2. `workOrders`: `available: false`, a user-facing unavailable explanation and `records: []`. The absence of this feature is distinguished from a functioning activity source with zero records.

`IMaintenanceReportSource.ReadAsync` is the integration seam. `UnavailableMaintenanceReportSource` is registered until System Integration connects PR #26. A future source should project PR #26's persisted entities with `AsNoTracking` into `MaintenanceRecordsSnapshot` and replace the registration in `Program.cs`. It must not create or mutate work orders.

`MaintenanceRecordRow` carries `maintenanceId`, `vehicleId`, optional `licensePlate`, `activity`, `status`, nullable `scheduledAt`, `completedAt`, `cost`, and `history`. History entries carry `changedAt`, `status` and optional `details`. Return `available: true` only when the persisted source is connected, preserve missing costs as null, and return an empty history array when none is recorded. The report UI already renders these records/history/costs when available. Adapter behavior is tested with supplied projection fixtures; that test is not a claim that PR #26's persistence exists on this branch.

PR #25: existing booking rows and all four enum statuses are reported now. After the lifecycle work merges, verify its status/reference semantics and persisted transitions against these read-only queries. No cancellation/completion logic is duplicated here.

PR #26: connect the maintenance projection adapter to persisted activities/history/costs and verify role access and recorded totals against real work orders.

PR #27: reporting has no notification dependency or required connection. When branches are integrated, preserve both registrations and staff navigation additions in `Program.cs`, `App.jsx` and `StaffSidebar.jsx`.

## Frontend behavior

The existing staff shell gains three role-gated views:

- `#/fleet-performance`: administrator fleet performance and operational statistics, summary cards and a refresh action.
- `#/booking-reports`: administrator booking table, status filter, pagination and refresh.
- `#/maintenance-reports`: administrator/manager/maintenance current vehicle table, activity/history/cost section and refresh.

Navigation and direct hash-route checks use the same report permission helper and check all assigned roles. The admin dashboard includes a fleet performance shortcut. Report pages load on demand through React's existing lazy/Suspense support, with no new library. Loading/error/empty/unavailable states are explicit. A changed session or query hides old data while a fresh request loads; obsolete requests are aborted. Report tables include captions and column headers. Existing business screens and application layout remain available.

## Tests and results (2026-09-29)

Added 50 backend tests (18 service cases, 32 HTTP cases) and 30 frontend tests (25 API/permission/formatting cases, 5 rendered component cases).

Backend coverage: persisted data across separate contexts; varying record counts; status aggregations; distinct utilization and start/end boundaries; retired/empty fleets; booking statuses/value/filtering/pagination; latest persisted updates; maintenance vehicle rows; unavailable versus available-empty work-order sources; future adapter history/cost projections; invalid filters; role matrix; JWT failure; cache headers; HTTP persisted record projections; unavailable database responses without invented zero totals or leaked details.

Frontend coverage: API paths and authorization headers; booking references/status/filter/page contracts; empty and unavailable data; fresh requests/session changes; HTTP 401/403/503 errors; cancellation signals; all role/view permissions; zero versus unavailable formatting; actual server-rendered summary cards/tables/loading/error/empty states using existing React/Vite tooling.

| Validation | Result |
| --- | --- |
| Focused report backend tests | 50 passed, 0 failed |
| Root `FleetFlow.sln` | Identity 101 + Vehicle 68 = 169 passed |
| `src/backend/FleetFlow.sln` | Identity 44 + Fleet 133 = 177 passed |
| Full backend total | 346 passed, 0 failed, 0 skipped |
| Frontend tests | 76 passed, 0 failed, 0 skipped |
| Combined full-suite total | 422 passed; focused rerun not double-counted |
| Backend compilation | Both solutions compiled successfully during full test runs |
| Frontend production build | Passed, 74 modules; initial JS 493.00 kB / gzip 120.96 kB; no chunk-size warning after report page splitting |
| Focused lint of changed/new frontend files | Passed |
| Full frontend lint | Two existing `react-hooks/set-state-in-effect` errors in unchanged `BookingModal.jsx:27` and `MaintenanceDashboardPage.jsx:57` |
| `git diff --check` | Passed |

Backend test evidence is kept under ignored `TestResults/reports-analytics/`. Service/HTTP tests use the repository's EF InMemory and WebApplicationFactory conventions. A real PostgreSQL dataset and interactive customer/staff browser acceptance remain System Integration checks. No commits or pushes were performed.

## File inventory

Modified:

- `src/backend/services/FleetService/FleetService.Api/Program.cs`
- `src/frontend/src/App.jsx`
- `src/frontend/src/components/layout/StaffSidebar.jsx`
- `src/frontend/src/pages/admin/AdminDashboardPage.jsx`

Added:

- `src/backend/services/FleetService/FleetService.Api/Controllers/ReportsController.cs`
- `src/backend/services/FleetService/FleetService.Api/Dtos/ReportDtos.cs`
- `src/backend/services/FleetService/FleetService.Api/Services/IReportingService.cs`
- `src/backend/services/FleetService/FleetService.Api/Services/ReportingService.cs`
- `src/backend/services/FleetService/FleetService.Api/Services/IMaintenanceReportSource.cs`
- `src/backend/services/FleetService/FleetService.Tests/ReportingServiceTests.cs`
- `src/backend/services/FleetService/FleetService.Tests/ReportsControllerTests.cs`
- `src/frontend/src/services/reportService.js`
- `src/frontend/src/utils/reportPermissions.js`
- `src/frontend/src/pages/reports/useReport.js`
- `src/frontend/src/pages/reports/ReportComponents.jsx`
- `src/frontend/src/pages/reports/FleetPerformancePage.jsx`
- `src/frontend/src/pages/reports/BookingReportsPage.jsx`
- `src/frontend/src/pages/reports/MaintenanceReportsPage.jsx`
- `src/frontend/src/pages/reports/Reports.css`
- `src/frontend/src/tests/reportService.test.js`
- `src/frontend/src/tests/reportComponents.test.js`
- `docs/reports-analytics.md`

Recommended commit structure, not executed:

1. Read-only report contracts, query service, maintenance integration boundary, authorized controller/registration and backend tests.
2. Staff/admin reporting pages, API client, permission helper, lazy routes/navigation and frontend tests.
3. Reporting architecture, metric definitions, integration requirements and validation documentation.
