# Desktop onboarding revision — 15 October 2026

This revision addresses manual feedback on Google registration, Google-only security settings, long account forms, administrator email decisions, email delivery visibility and report usability. The owner accepted the updated local version and authorised its `develop` pull request on 15 October 2026. Earlier port 5173 and Azure resources remain unchanged.

## Changed customer flows

- Both customer authentication pages offer Google before the ordinary form. An existing linked Google subject signs in from either page. A new subject returns `authenticationStatus: ProfileRequired` with a short-lived opaque `signupId`, verified email and suggested name; it receives **no login token or account** yet.
- `POST /api/onboarding/google/signup/complete` accepts that server-held proof, the chosen username and full name. It checks expiry, replay, current uniqueness and CUSTOMER-only ownership before creating the account. Non-authoritative Google email still needs FleetFlow OTP. A matching local email is never automatically linked.
- Welcome email is queued only after successful account creation, for both Google and OTP signup, with the person's chosen full name. Existing Google sign-in does not create another account or welcome message.
- Google-only settings use authenticated `google/security/begin` and `google/security/complete`. The server checks the linked Google **subject**, nonce, expiry and current account; it issues a five-minute single-use grant bound to that account/access version. The grant authorises a subsequent request, never replaces required email OTP.
- Email change retains the old email until the new-address challenge completes. Already enabled login verification still requires its current-address step. Optional password creation uses `password/set` followed by authenticated `password/set/confirm`, verifies an email code, increments access version and returns a replacement session. Google sign-in remains linked.
- New customer two-step enrolment is omitted from this interface for the viva. Existing enrolled customers retain its controls and enforcement; staff retain optional enrolment. Initial email verification, recovery, email changes and deletion requirements remain intact.

## Desktop navigation and motion

The account header retains photo controls and opens Security through **Manage security**. A side navigation selects Overview, Personal details, Driving verification, Security or Account deletion. Only one panel renders at a time. `section` in the hash retains context across refresh and browser Back. Booking eligibility failures link directly to Driving verification and preserve the existing vehicle/date intent.

Google's official button is contained in the form column, with name/username completion after Google verification. The inherited artwork top offset has been removed. Username/email feedback has neutral checking, green available, red unavailable and a distinct failed-check message. A repeated previous value starts a new check rather than reusing a stale success.

Motion uses the existing GSAP dependency: a short route/dot reveal in authentication artwork and a subtle account-heading entrance. Selection/hover feedback uses CSS. Essential content stays visible, forms and report actions stay stationary, and reduced-motion mode skips these effects. Contexts and callbacks are cleaned up on navigation; a late Google callback cannot link an account after its component is gone.

Research: [21st.dev Tabs](https://21st.dev/originui/tabs), [shadcn Tabs](https://ui.shadcn.com/docs/components/base/tabs), [GSAP matchMedia](https://gsap.com/docs/v3/GSAP/gsap.matchMedia%28%29/) and [GSAP Flip](https://gsap.com/docs/v3/Plugins/Flip/). The account navigation and selection treatment are original CSS/React adaptations within FleetFlow, not copied paid template code. No extra animation framework or paid generation service was added. Existing component licences remain in Credits.

## Administration and reporting

Account administration now separates Email verification, Email delivery and Deleted accounts. Verification is searched by account, exposes its current state and opens a contextual reason/confirmation dialog. Already verified accounts cannot be downgraded into a waiver. Administrator approval remains visibly distinct from owner email verification and does not override driver review, expiry, uniqueness or optional login verification.

Email delivery has global status summaries, server-side 25-row pagination, status filters, readable message purposes and masked recipients. Error explanations, attempts and timestamps are expandable. Retry remains limited to failed unexpired payloads. Expired OTPs cannot be resent through administrator replay. SMTP acceptance is never labelled proven inbox arrival.

`GET /api/email-deliveries/workspace` is additive; the previous list endpoint remains available. `GET /api/onboarding/accounts/email-status` is ADMIN-only. Masked recipient hints are retained after sent OTP ciphertext is cleared; anonymisation removes those hints for account-owned jobs too. No codes, message bodies or private identity documents are exposed through the workspace.

The dashboard adds actionable counts for pending driving reviews, initial email verification and failed messages. Report pages gain consistent navigation, status filters, short linked references, customer names from the existing administrator directory, clear estimates/cost labels and explanatory details. Booking summary cards describe **all bookings** while table counts describe the selected filter. Maintenance filter totals describe its displayed activities; its CSV intentionally exports the complete report and says so. No synthetic financial trends or payments are invented.

## Deployment requirements

Deploy compatible Identity, Notification and frontend versions together under the existing controlled activation/write-pause procedure. The new Google registration response needs the matching frontend; do not mix it with the earlier prefilled-username Google form.

Notification activation adds `EmailJobs.RecipientHint` as `text NOT NULL DEFAULT ''`. Email readiness checks for that column when `EMAIL_ENABLED=true`. Startup applies the additive change with its existing email schema setup; the deployment database role must be allowed to apply it, or the migration must be prepared by DevOps. Retain all existing encryption keys and persistent private files. This does not replace the four-service ownership migration receipts.

Keep the exact deployed frontend origin in CORS and Google Web Client origins. Use HTTPS public API roots at build time and the deployed `FRONTEND_PUBLIC_URL` for email actions. No App Password, client secret, data key or private local environment file belongs in Git or a frontend bundle. Google needs its configured Client ID; no client secret is used by this implementation.

Before release, manually confirm existing and new Google paths, real OTP receipt, personalised welcome email, Google-only settings, account navigation, administrator verification and deployed report/export permissions. The owner accepted the local version overall; this does not establish each provider/mailbox case on Azure. S4-12 stays open until deployed QA succeeds.

## Safety and evidence

The pre-revision source archive is `Z:\Programs\FleetFlowCaches\FrontendBackups\onboarding-before-review-20261015-070354.zip`: all 656 archived source/config/licence/screenshot files were SHA-256 checked against their originals. Dependencies, builds and ignored secrets are excluded. The full manifest is in the project mirror's `verification/onboarding-before-review-backup.json`. Keep private keys separately in their existing protected configuration.

Restore source into a separate verified checkout first. Once onboarding writes exist, do not switch feature flags off or overwrite current databases to bypass new rules; preserve keys/history and reconcile forward. The earlier local environment remains the immediate visual/testing fallback. See [local acceptance](onboarding-manual-review.md), [results](onboarding-review-results.json) and [DevOps handoff](verified-onboarding-handoff.md).
