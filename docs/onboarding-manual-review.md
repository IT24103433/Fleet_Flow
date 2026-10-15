# Onboarding: local manual acceptance

Review this feature at **http://127.0.0.1:5174/**. The earlier Sprint 4 site remains separately available at **http://127.0.0.1:5173/**. Use separate browser tabs or profiles for customer and administrator sessions; sessions in a tab change when you switch accounts.

The isolated review owns separate PostgreSQL/Kafka containers and private document storage. It contains copies of existing history plus labelled synthetic review accounts. Source databases and Azure resources were not changed. Do not upload real driving documents for development testing.

## Start with these flows

1. **Email signup:** use an unused address from the selected inboxes below. Enter only full name, username, email, password and confirmation. Check the username/email availability feedback, request the code, and enter it directly on the site. No account session should be created before verification. If the address is already registered, sign in or use recovery instead.
2. **Google:** select your Google account first from either sign-in or signup. Existing linked accounts sign in directly. New Google accounts open a separate screen for full name and an available username after identity verification; no local password is requested. Driving approval still comes before new bookings. Existing password accounts link Google only from authenticated Security settings.
3. **Driving verification:** Account → driving details. Choose the licence-issuing country. Check Sri Lankan NIC/passport and foreign licence/passport/IDP variants. Save a draft; upload small synthetic JPEG/PNG/WebP files; submit. Verify pending/rejected/correction states. Changing approved legal details requires a new review.
4. **Administrator:** use the private synthetic administrator account below. Open Driver reviews, inspect private photographs, and approve/reject with a reason. Duplicate approved NIC/DL claims and expired evidence cannot be overridden. Use separate customer accounts to check isolation.
5. **Booking:** an unapproved customer must be blocked, including direct API creation. After approval, return to the selected vehicle/dates and submit deliberately. Check documents that expire before the return date. Existing bookings, cancellation, pickup/return and reporting remain available.
6. **Profile/security:** Account → Personal details edits name, username, phone and address. Account → Security manages email/sign-in. Google-only accounts confirm their linked Google identity before an email change or optional password creation, followed by the required email code. The old email stays active until the new one is verified. New customer two-step enrolment is hidden for the viva; existing enrolled customers retain controls/enforcement, and staff retain optional enrolment.
7. **Customer lifecycle:** try a homepage support enquiry. With no future booking or unfinished rental, request deletion and verify it by email. Access is revoked immediately. The administrator can restore from Account administration → Deleted accounts within thirty UTC days, with a reason. Old sessions must stay invalid.
8. **Staff:** use Staff Portal to confirm its distinct operations artwork and the “Enter your user name” placeholder. New staff must verify their initial email and retain any forced-password-change requirement. Administrator email waivers require a reason and do not waive driver review, expiry or optional login verification.

## Test inboxes and private accounts

Local mail is intentionally limited to these configured inboxes:

- `jeyanthansaran@gmail.com`
- `jimmyxmangala@gmail.com`
- `jimmyxsaran@gmail.com`
- `fleetflow.automations@gmail.com`

An address outside that list is rejected immediately during local challenge creation. Production uses its separately configured delivery policy. Check spam/junk folders and the administrator Email delivery panel if mail does not arrive. `Sent` means SMTP accepted the message, not proof of inbox delivery. Never paste codes/passwords in chat.

Synthetic account usernames are `qa_live_admin`, `qa_live_customer` and `qa_live_other`. Their generated passwords are stored only in the Git-ignored file `ReviewResults/onboarding/private-review-accounts.json` in the `verified-onboarding` checkout. Open that file locally; do not copy it into Git, screenshots or the handoff. These accounts have synthetic `example.invalid` addresses and cannot receive email; use selected real inboxes for OTP/recovery/security/deletion acceptance. The synthetic customer's document approval is explicitly marked as test evidence, not genuine identity verification.

The earlier Google integration was confirmed by the user, and the updated local version was accepted overall on 15 October 2026. Individual provider/mailbox results were not supplied as a complete acceptance matrix. Verify the Google-first/profile-completion flow, real OTP receipt and personalised welcome emails on the deployed release. Automated signed-token/OTP tests and SMTP acceptance do not replace provider/inbox checks. Delivery contains deliberately blocked synthetic messages: outside-the-local-test-list errors are expected for example.invalid addresses.

## Resume and fallback on this laptop

The project mirror's `verification/Resume-OnboardingReview.ps1` resumes only this named review stack and port 5174, preserving its existing encryption keys, databases and files. It reloads selected SMTP/Google settings from the ignored private environment file. Open Docker Desktop first. Do **not** rerun `Start-OnboardingReview.ps1` over occupied ports; that helper creates a fresh isolated clone.

`verification/Stop-OnboardingReview.ps1` stops only the review processes, retaining its data. Neither helper deletes volumes or changes the earlier manual environment. If the new UI needs revision, use port 5173 as the existing local fallback; do not downgrade the newly written onboarding databases.

## Acceptance and deployment boundaries

Desktop review uses a 1440 × 900 viewport; full-page screenshots retain the page's vertical content. Dedicated mobile/tablet review is intentionally omitted. Review evidence is in [onboarding-review-results.json](onboarding-review-results.json), with screenshots under `screenshots/onboarding`.

These are local development results, not production acceptance. The owner has authorised the `develop` pull request after accepting the local version; merges and deployment remain owner/DevOps actions. Deployment requires the compatible services, additive schema activation, private persistent storage, preserved encryption keys, SMTP configuration, Google origins and exact frontend API build settings described in [verified-onboarding-handoff.md](verified-onboarding-handoff.md). S4-12 remains open until the actual deployed workflows pass.
