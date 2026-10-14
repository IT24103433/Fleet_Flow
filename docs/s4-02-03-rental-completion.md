# S4-02 and S4-03: Vehicle return and rental timeline

Admins and Fleet Managers record a vehicle's actual return, mileage, fuel percentage, condition and any agreed additional charges. Customers see their own handover receipt and booking timeline. Completed and cancelled records remain accessible through the staff rental queue; existing booking-only customer links also load saved history.

The original booking estimate is preserved. Final amount equals that estimate plus explicitly entered additional charges; a positive charge requires a reason. Return time is captured by the service. Lateness is flagged without inventing an automatic fee policy. Mileage cannot go backwards, amounts support at most two decimal places, and completed handovers cannot be submitted again.

## Durable completion and availability

Rental saves an immutable return intent under the existing booking lock before contacting Fleet. Fleet uses the same vehicle lock as bookings, maintenance and pickup to record the matching return and release the active hold. It advances vehicle mileage and makes the vehicle Available, or Maintenance if existing scheduled/in-progress maintenance conservatively blocks it. Rental then commits Completed state, booking status, the completion audit moment and a durable notification outbox event together.

Interrupted calls retain CompletionPending and the original actor, timestamp and submitted details for recovery. Conflicting resubmissions are rejected; definitive pre-commit validation failures restore Active so details can be corrected. Cancellation remains blocked. Fleet's identical retry acknowledgement never changes a vehicle already handed over to a later rental. After Fleet durably accepts return, its availability can be released while Rental is still reconciling a lost acknowledgement; staff must refresh the pending record instead of creating a second return.

Timeline reads use Rental's own database and work during a Fleet outage. New confirmation, cancellation and completion moments are saved in their business transactions. Historical confirmation times are explicitly unknown; historical completed records do not receive fabricated pickup/return details. Last-update timestamps are described accurately where legacy history lacks a dedicated event.

## API contracts

| Method / path | Access | Result |
| --- | --- | --- |
| POST `/api/rentals/{rentalId}/complete` | Admin / Fleet Manager | Rental receipt, separate original/final amounts; 400 invalid details, 404 missing record, 409 invalid/repeated state, 503 unresolved remote operation |
| GET `/api/bookings/{bookingId}/timeline` | Owning Customer / Admin / Fleet Manager | Ordered saved history, nullable unknown timestamps |
| GET `/api/rentals/queue?state=Completed` | Admin / Fleet Manager | Completed rental and historical booking records |
| GET `/api/rentals/queue?state=Cancelled` | Admin / Fleet Manager | Cancelled booking history |
| POST `/internal/reservations/{bookingId}/complete` | Internal reservation-write permission only | Idempotent acknowledgement for the original rental, mileage and timestamp |

Fleet provides the fixed compatibility paths only in Remote ownership mode. Browser user tokens cannot invoke internal commands. Existing ownership checks, forced-password restrictions, booking estimates, response fields and booking status names remain intact. Return details and final amount are additive fields. Completion notifications use the existing booking topic, producer outbox, retry and inbox deduplication.

## Schema 4, activation and rollback

Fresh imports install rental schemas 1–4. Existing schema-3 installations require the coordinated additive upgrade below; service readiness requires the verified schema-4 owner/checksum and import nonce. Default Legacy ownership and inactive new services are unchanged: deploying these artifacts does not automatically cut over.

1. Pause booking, handover and maintenance writes. Take fresh auth, Fleet and Rental database backups plus uploads; restore them into isolated verification databases and record hashes in the existing verified backup receipt format. Never verify by restoring over working databases.
2. Export the owned Rental booking snapshot using `RentalMigration --mode export-target --file <new-export.json>`. Preserve its SHA-256. Configure explicit source/target database settings for the verified installation; credentials stay outside Git.
3. Run `RentalMigration --mode upgrade-rentals --file <export.json> --sha256 <hash>` first as a dry run. Apply with `--backup <verified-receipt.json> --write-pause confirmed --apply true`. The existing `upgrade-pickup` alias now also installs schema 4. Both require matching export, nonce, schema ownership/checksums and restoration-verified backup artifacts before writes.
4. The tool adds Fleet return-ledger fields and Rental return/audit tables without rewriting legacy booking, vehicle, maintenance or image records. Repeating the verified upgrade is safe; checksum or owner mismatches fail closed.
5. Deploy compatible Fleet, Rental and Notification artifacts together, verify readiness and permissions, then reopen writes. Keep original tables and verified backups. Azure provisioning and production cutover remain DevOps responsibilities.

After any pickup or return intent exists, **do not switch to Legacy, deploy earlier lifecycle-incompatible Fleet/Rental executables, clear holds/history, or restore old working database dumps**. Keep compatible services and reconcile persisted workflows under a write pause. A data-preserving rollback to older behavior requires a separately reviewed lifecycle-aware migration; the legacy reconciliation tool refuses pickup history. A frontend-only rollback does not undo vehicle availability, final amounts, bookings or notifications.

The local rehearsal restored the already verified backup into four strictly named disposable databases, imported four bookings and sixteen notifications, and used copied uploads. It did not activate or migrate the working databases, alter the deployed fleet, or modify the shared PostgreSQL/Kafka infrastructure. Only its recorded, path-verified service processes and disposable database names were cleaned up.

## Design acceptance

Reviewed at 1440 × 900: stable return controls, guarded focus-contained confirmation, completed receipt, record reload and the five-step saved timeline, with no page-wide horizontal overflow. Return forms appear before expandable pickup history so staff can act immediately. The final receipt appears before the same expandable history. Both original and final amounts remain readable. Customer access was verified against the owned-record API.

The timeline adapts [Aceternity's Timeline composition](https://ui.aceternity.com/components/timeline) using existing CSS/GSAP and the retained `public/licenses/aceternity.txt`; attribution is in Credits. Only its decorative rail animates. Content remains immediately available; reduced-motion users receive the complete static layout, and navigation removes media-query listeners and animation contexts. Confirmations reuse the existing tested Modal and [21st.dev shadcn alert-dialog pattern](https://21st.dev/%40shadcn/components/alert-dialog/with-icon); icons remain Phosphor. No new animation framework is added.

Screenshots: `review/s4-return-record-1440.jpg` and `review/s4-return-timeline-1440.jpg`. Dedicated mobile/tablet testing was skipped as requested.

## Regression acceptance

- Preserved the 566 backend / 140 frontend baseline from pickup. Full checks passed **575 backend tests and 144 frontend tests**, without skips. Added coverage exercises return validation, exact lateness boundaries, original estimates, manual charges, concurrent requests, lost acknowledgements, restart recovery, local outbox transaction failure, later-pickup protection, schema upgrades, historical timeline truthfulness, permissions, duplicate submissions and reduced-motion cleanup.
- Focused UI tests (12/12), lint and production build passed after the desktop layout adjustment. All four APIs and both migration tools package successfully. No backend deployment was performed.
- Four-service rehearsal completed through Browser: original estimate LKR 9,000, staff charge LKR 500 with a reason, final amount LKR 9,500, booking/lifecycle Completed, odometer 5,500 km, vehicle Available, five timeline entries and exactly one completion notification. Customer completion returned 403 and repeated staff completion returned 409. Retained legacy booking count remained four.
- Entry plus recursively static imports is **308,893 uncompressed bytes**, below 350,000. Additional lazy closures are measured separately in `s4-02-03-verification.json`; marketing and timeline animation code stays outside the entry closure.

This PR implements S4-02 (5 points) and S4-03 (2 points). Maintenance scheduling/assignment/audit, access revocation, final API hardening, final integration and actual deployment verification remain separate work.
