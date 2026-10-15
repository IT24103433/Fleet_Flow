# Verified onboarding and account lifecycle — local review / DevOps handoff

This change extends the four-service Sprint 4 release. The owner accepted the updated local version and authorised a pull request into `develop` on 15 October 2026. It does not close S4-12, merge branches or deploy Azure resources. The earlier website remains available separately.

The [15 October desktop revision](onboarding-desktop-revision.md) adds Google-first profile completion, Google-only reauthentication, account subsections, contextual administrator decisions and clearer reports. Use its matching frontend/API versions together.

## Service ownership and interfaces

| Service | Added ownership |
|---|---|
| Identity | Pending signup, purpose-bound email challenges, Google subject links, editable contact profiles, encrypted driver revisions/photos, NIC/DL ownership claims, review audit, security settings and the thirty-day customer recycle bin |
| Notification | Encrypted transactional email jobs, SMTP retry/history, event-to-email rendering and saved support enquiries with validated Reply-To |
| Rental | Current driver eligibility before new booking intent, account obligations and a customer booking freeze that serialises with new booking creation |
| Fleet | Existing vehicles, reservations, maintenance and composed reports; no identity/document copies are introduced |

Public Identity APIs are under `/api/onboarding`: `config`, `availability`, `signup`, `verify`, `resend/{id}`, `google/begin`, `google/complete`, authenticated `google/link/*`, `profile`, `verify-email`, `driver`, `driver/draft`, `driver/submit`, `driver/photos/*`, `eligibility`, `recovery`, `recovery/reset`, `email-change`, `second-step/{enable}`, `deletion`, and `deletion/confirm`. Administrator-only routes are `reviews`, `reviews/{revision}`, `email-waiver/{account}`, `recycle-bin`, and `recycle-bin/{account}/restore`.

Verification-required login returns a challenge and **no usable login token**. Successful signup/login returns the existing session shape. Sensitive settings return a replacement `session`; enabling email login verification also returns eight recovery codes once. Email change retains the old address until successful verification. With optional login verification enabled, email change verifies the old address before the new one. Initial staff verification cannot be replaced with a recovery code.

Notification exposes anonymous rate-limited `POST /api/support`, administrator `GET /api/email-deliveries` and guarded replay, plus authenticated internal enqueue/anonymisation. Support enquiries are durable encrypted `EmailJobs` with template `Support`; replies remain manual. Inbox delivery and SMTP delivery are independent. SMTP acceptance is recorded as `Sent`, which does not prove inbox arrival. A connection failure after SMTP acceptance can produce a duplicate email on retry; stable Message-ID is retained, but SMTP is not an exactly-once protocol.

Rental exposes scoped internal account-obligation, freeze, restore and anonymisation contracts. Identity is the only client granted these lifecycle scopes. Browser tokens cannot call them. Rental acquires the same customer database lock for freeze and new booking workflow creation; pending cross-service workflows and unfinished handovers block deletion. Historical completed/cancelled bookings and financial records remain intact.

## Local manual review

Use the step-by-step [manual acceptance guide](onboarding-manual-review.md) and [recorded local results](onboarding-review-results.json). The owner accepted the local version overall; individual provider/mailbox outcomes were not supplied as a complete acceptance matrix. Confirm actual OTP receipt and completion on the deployed release; do not interpret SMTP acceptance or automated challenge tests as proof that a customer received the message.

- New review: `http://127.0.0.1:5174/`, APIs `5011–5014`. Earlier manual environment: `http://127.0.0.1:5173/`, APIs `5001–5004`.
- The startup helper clones source auth/Fleet databases through read-only dumps, verifies isolated restores/content fingerprints and upload hashes, then runs guarded Rental/Notification imports. It does not migrate or overwrite original databases.
- Local delivery is restricted to selected test inboxes. A different signup address returns an immediate local restriction error. Production must leave `LOCAL_EMAIL_ALLOWLIST_REQUIRED=false`; Development Notification always applies `EMAIL_ALLOWLIST`.
- Use synthetic document photos/identifiers. Never upload real documents solely for development review. New accounts need administrator driver approval before new bookings; existing history, cancellation and handover remain accessible.
- Google signup requires an available chosen username and legal driving review afterwards. Google-only customers can optionally set a local password from Security after fresh linked-Google confirmation and an email code; verified recovery remains available. Existing local accounts require explicit linking; matching emails never auto-merge accounts.
- Check contact edits, OTP expiry/resend, rejection/correction, document expiry through return, normal and optional second-step login, recovery, email changes, support, deletion and administrator restoration. Only administrators can see the recycle bin.

## Configuration by service

Keep existing database/JWT/Kafka/ownership/receipt settings from the Sprint 4 DevOps README. Supply secrets through protected App Service settings or Key Vault references, not frontend variables, Git or deployment packages.

| Service | Settings |
|---|---|
| Identity | `ONBOARDING_ENABLED=true`; distinct base64 32-byte `ONBOARDING_DATA_KEY` and `ONBOARDING_HASH_KEY`; `SERVICE_AUTH_IDENTITY_SECRET` of at least 32 bytes; trusted `INTERNAL_RENTAL_URL` and `INTERNAL_NOTIFICATION_URL`; `FRONTEND_PUBLIC_URL`; `IDENTITY_DOCUMENT_ROOT`; optional `GOOGLE_CLIENT_ID` |
| Notification | `EMAIL_ENABLED=true`; separate base64 32-byte `EMAIL_DATA_KEY`; `SMTP_USERNAME`; fresh Gmail App Password in `SMTP_PASSWORD`; `SUPPORT_INBOX`; `FRONTEND_PUBLIC_URL`; existing internal Identity/token settings |
| Rental | `REQUIRE_DRIVER_VERIFICATION=true`; existing Identity/token settings; verified active Rental ownership/schema 4 |
| All APIs | Exact HTTPS frontend origin in `ALLOWED_ORIGINS`, trusted HTTPS internal endpoints and correct proxy/TLS configuration |

Google must authorise `https://fleetflow-frontend.azurewebsites.net` as a JavaScript origin. The Client ID is public; this implementation does not use a Google client secret. Use separate local and production OAuth clients in line with Google's production guidance. FleetFlow uses Google Identity Services authentication only, requesting no Gmail, Drive or other API permissions. Google's [basic identity exception](https://support.google.com/cloud/answer/15549945?hl=en) allows Sign in with Google users outside the test-user list, even in Testing mode. This exception does not apply if additional scopes are requested. Configure an External audience for customers outside your organisation; Workspace administrators and account policies can still block access. Review production branding/publishing settings before public launch.

New Google subjects are supported: Google verification is followed by a chosen full name and available username, then CUSTOMER-only creation (and FleetFlow OTP for a non-authoritative provider email). Existing linked subjects sign in from either customer page. A matching local email requires explicit linking after local authentication. Before deployment sign-off, use an unused Google account to exercise this path on the deployed origin, verify the welcome email and check that driver approval still gates booking. Local email delivery is separately allowlisted; do not copy that restriction into production.

Production builds explicitly provide `VITE_IDENTITY_API_URL`, `VITE_FLEET_API_URL`, `VITE_RENTAL_API_URL` and `VITE_NOTIFICATION_API_URL`. CI/manual frontend deployment uses repository variables with the currently verified Azure hosts as defaults. Build validation rejects missing roots, localhost/private destinations, credentials, paths and non-HTTPS URLs. Vite values are **build-time** values. A deliberate local review build requires all four loopback roots and `FLEETFLOW_LOCAL_REVIEW_BUILD=1`; this exception is disabled in CI.

Private documents must be in persistent restricted storage outside `wwwroot` and public `/uploads`. For Linux App Service use a provisioned persistent private path, for example `/home/data/fleetflow-private-documents`, with the correct persistence/mount settings for the chosen hosting mode. Preserve encryption keys with encrypted document/database backups. Replacing keys without a migration makes existing protected records unreadable. Profile/vehicle uploads keep their existing persistent locations.

Gmail uses `smtp.gmail.com:587` with TLS and sender name **FleetFlow Automations**. Use a fresh App Password after two-step verification; rotate credentials previously exposed in chat. Gmail quotas, spam classification and delivery delays remain external constraints. Configure a suitable transactional provider for larger public usage rather than promising Gmail has unlimited delivery. No payment receipts/refunds/tax invoices are invented.

## Migration and activation

1. Verify the existing four-service release and its migration receipts first. Take database/upload/private-document backups and verify isolated restoration. Do not resolve a missing schema by changing activation flags or bypassing the guarded migration tools.
2. Audit case-insensitive trimmed username/email collisions before activating onboarding. Resolve legitimate ownership explicitly. Activation creates PostgreSQL unique indexes and fails readiness if conflicting old data exists; administrators cannot override collisions.
3. Deploy compatible Identity/Notification/Rental/Fleet artifacts under a controlled write pause. Identity adds user verification/deletion/password-provider fields plus `IdentityOperations`, `DocumentClaims` and `ExternalAccounts`. Notification adds `EmailJobs` and masked `RecipientHint`; email readiness requires that column. Rental adds `AccountBookingGuards`. These changes are additive and preserve history/IDs.
4. Install private storage and durable keys/SMTP/Google/internal settings. Enable email, Identity onboarding and Rental verification together. Keep one active writer per owned domain. Existing active administrators receive a one-time audited initial email waiver, not a driver/MFA/recovery bypass.
5. Build/deploy the frontend with its four public HTTPS API roots. Confirm readiness and actual protected workflows before reopening writes. Google missing configuration hides its button rather than displaying a broken integration.
6. Record actual deployed results separately: OTP signup/staff first login, Google, private upload permissions, approval/duplicate rejection/expiry, booking enforcement, password/email changes, optional login verification, support and deletion/restoration. Continue the existing Sprint 4 booking/maintenance/report checks. Only actual deployment evidence closes S4-12.

## Deletion, retention and rollback

Deletion needs a customer email code and resolution of future confirmed/pending bookings and uncertain/active rentals. Rental freezes new booking creation before Identity marks deletion/revokes access. A fresh owner-verified deletion can reconcile an interrupted freeze. Administrators restore only within thirty UTC days, with a reason and Rental acknowledgement; old sessions remain revoked.

After thirty days the retrying lifecycle worker removes encrypted driver files/claims/provider links, removes the owned profile photo when safely identifiable, anonymises Identity contact data and Notification personal payloads, and preserves necessary IDs/amounts/history in Rental/Fleet. Restored accounts retain their claims. Removed or superseded photos are covered by the account's private photo index. Expired OTP payloads are cleared; reminders run once per latest approved revision at seven/one-day boundaries.

Backups and Kafka are not silently rewritten. DevOps must define restricted access, finite retention, secure disposal and a restoration procedure that reapplies the deletion/anonymisation ledger before exposing restored data. Historical Identity Kafka events can contain personal contact fields; retention/handling must be included in the production policy. Anonymous support data has its own retention policy and is not automatically equated to an account by name. This needs documented operational ownership before public launch.

The earlier local environment is the review fallback. Once production has new Google-only accounts, challenges, approvals, MFA or lifecycle writes, do not downgrade binaries or switch feature flags off to bypass the rules. Pause writes, preserve current keys/history, restore into isolated verification databases and reconcile forward. Database restoration must not reactivate deleted users or release still-owned document claims.
