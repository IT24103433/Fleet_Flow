# S4-04–S4-06: Maintenance windows, assignment and audit history

Staff can plan a maintenance start/end window, change a scheduled window, assign active Maintenance Staff and inspect saved activity. The existing desktop workspace, work-order details, status transitions, cost reporting and CSV permissions remain intact. This grouped PR delivers S4-04 (4 points), S4-05 (2 points) and S4-06 (2 points).

## Availability rules

Planned windows use half-open intervals: a booking ending at maintenance start, or starting at maintenance end, is permitted. Overlaps conflict. Durable maintenance records and Fleet's booking reservation ledger are checked under the same vehicle-row lock, including concurrent scheduling, booking, pickup and return commands. Fleet remains the authoritative availability owner; Rental never queries maintenance tables.

Known scheduled windows leave a physically idle vehicle Available, while the existing date-availability endpoint rejects conflicting booking periods. Starting work within its planned window changes the vehicle to Maintenance. In-progress work blocks availability until explicitly completed/cancelled, even if its planned end passes. It cannot start while another actual/unknown-end work order or active rental blocks the vehicle. Return respects actual/unknown-end maintenance while allowing availability outside future planned windows.

Existing records and older clients without an end time retain conservative blocking. Their end time is not fabricated. Staff can convert a scheduled legacy record to a verified window through Plan window. Active rentals, retired vehicles, invalid windows, conflicting bookings and overlapping maintenance are refused. Scheduling accepts at most 365 days per planned window. Cancelling/completing work recalculates physical status without overriding InUse or Retired.

## Assignment and history

Admins and Fleet Managers assign, reassign or clear work on non-terminal records. Fleet retrieves and revalidates the selected active MAINTENANCE_STAFF account through Identity's restricted internal API using the existing short-lived service token and identity.staff.read permission. Browser user tokens do not satisfy that internal policy. An Identity outage produces 503 and saves no assignment; there is no fabricated directory or direct access to Identity tables. Validation reflects account status at the command's validation point, not a distributed lock across both services.

The creator remains unchanged. The stored assigned name is the validated full name, with its own username fallback; it is a snapshot at assignment time. The UI never substitutes another account's profile photograph.

Creation, detail edits, window changes, assignment and status changes append actor, UTC time and before/after snapshots in the same Fleet transaction as the work-order change. Notification events remain in the transactional outbox. Audit rows have no update/delete API; EF guards reject tracked changes and PostgreSQL rejects raw updates/deletes through an append-only trigger. Reports and maintenance CSV history use these saved entries. Historical records without audit entries show an explicit empty history rather than reconstructed events.

Activity is displayed newest first. Dates, costs and statuses are readable, unchanged null fields are omitted, and the initial creation snapshot is expandable. Forms and buttons remain stationary; dialogs retain the existing focus containment, neutral close action and duplicate-save guards. Phosphor icons, workspace typography and existing attributed dialog/timeline patterns are reused without adding dependencies or a new animation framework.

## API and ownership

| Method / path | Access | Purpose |
| --- | --- | --- |
| POST `/api/maintenance/records` | Existing Admin / Fleet Manager / Maintenance Staff access | Optional additive plannedEndDateTime; omitted ends preserve legacy behavior |
| PUT `/api/maintenance/records/{id}/schedule` | Existing maintenance roles | Required start/end; scheduled work only |
| PUT `/api/maintenance/records/{id}/assignment` | Admin / Fleet Manager | Required assignedStaffId, nullable to clear |
| GET `/api/maintenance/records/{id}/audit` | Existing maintenance roles | Saved immutable activity; 404 missing record |
| GET `/api/maintenance/staff` | Admin / Fleet Manager | Active directory; no-store and explicit unavailable state |
| GET `/internal/staff/maintenance` and `/{id}` | Internal identity.staff.read only | Identity-owned role/status validation, no password/contact fields |

The original maintenance endpoints remain valid. Actors come from authenticated claims, not request bodies. Directory and assignment APIs fail closed; list responses are bounded to 1,000 staff. No service ports, databases, booking responses, authentication policy or deployed fleet are changed.

## Upgrade and rollback

Fleet's existing initialization path now installs additive PlannedEndDateTime, AssignedStaffId and AssignedStaffName fields plus MaintenanceAuditEntries and its append-only trigger, through MaintenancePlanningSchema.EnsureAsync. Existing nullable fields remain unknown/unassigned. No old rows or operational statuses are rewritten. Fresh EF-created databases include the new model; the SQL helper also supports existing installations and safely repeats. Fleet readiness is marked only after this schema installation succeeds.

Before DevOps upgrades, pause maintenance/booking/handover writes, create fresh Fleet/auth/Rental database and upload backups, verify hashes and restore them into isolated verification targets. Deploy compatible Fleet and Identity artifacts together; configure SERVICE_AUTH_ENABLED and the existing private signing/client keys and INTERNAL_IDENTITY_URL. Assignment must not be enabled by weakening the internal audience or falling back to public user-list calls. Confirm readiness, directory permissions and overlap rules before reopening writes. Migration, service provisioning and deployed acceptance remain DevOps/QA responsibilities.

After any new window or audit is saved, earlier Fleet binaries are not a safe semantic rollback: they cannot honor planned availability or append required audit history. Retain columns/tables/history and reconcile through the compatible implementation. Do not restore old database dumps over later bookings or handovers. A frontend-only rollback preserves server restrictions and saved history; any older-backend rollback requires a separately reviewed data-preserving migration under a write pause. Audit records must not be deleted to undo changes.

Local verification restored the previously hash/restore-verified auth/Fleet backup and copied uploads into four strictly named disposable databases, then imported four bookings and sixteen notifications. It created only an isolated demonstration work order and booking. The original working databases, booked Lamborghini, shared Kafka/PostgreSQL infrastructure and Azure fleet were untouched. Only path-verified review processes and exact UUID-suffixed verification databases were cleaned up.

## Acceptance evidence

Design acceptance at 1440 × 900 covered the work-order table, actual staff directory, assignment confirmation/result, rescheduling and readable activity history. No page-wide horizontal overflow occurred. Screenshots are `review/s4-maintenance-workspace-1440.jpg` and `review/s4-maintenance-history-1440.jpg`. No dedicated mobile/tablet testing or optimization was performed.

Functional evidence includes boundary-touching bookings, rejected overlaps (409), customer assignment rejection (403), active/legacy blocking, actor/creator preservation, invalid/inactive staff rejection, Identity outages, duplicate UI saves, concurrent booking/maintenance with exactly one winner and transactional rollback when audit persistence fails. A Browser-saved assignment and reschedule produced Created, Assigned and Rescheduled entries; the vehicle remained Available outside the planned window. Existing maintenance reports contained all three entries and CSV returned 200. Retained legacy bookings remained four.

The initial full local run passed 610 backend tests and 155 frontend tests. A later PostgreSQL staff-directory regression test was added after the live rehearsal exposed a relational projection issue. Final local confirmation passed the six other assemblies (566 tests, including that new PostgreSQL check); Windows Application Control blocked loading the Rental test assembly. Its 45 tests had passed in the earlier full run. Windows protections were left unchanged; the final complete 611-test backend confirmation is delegated to the PR's Linux CI. Final frontend tests, lint, production build and diff checks passed. Exact results and lazy-route byte measurements are in `s4-04-06-verification.json`.

Entry plus recursively static imports remains below 350,000 uncompressed bytes. Marketing, rental timeline and maintenance workspace chunks are measured separately. Access revocation/API hardening (S4-08/S4-09), final integration/handoff (S4-11) and deployed verification (S4-12) remain separate work.
