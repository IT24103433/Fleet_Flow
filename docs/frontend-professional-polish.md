# FleetFlow frontend redesign — review notes

Visual direction superseded by [Aura desktop review](aura-redesign-review.md) on 13 October 2026. The functional baseline described below remains preserved.

FleetFlow now has a coordinated automotive website and operations workspace: white and light-grey surfaces, charcoal text, blue accents, Inter typography, and direct English copy for Sri Lankan renters. The existing React/Vite stack, service integrations, role restrictions, and prior functional fixes are preserved. The work remains local on `feat/frontend-professional-polish` for the developer's review and PR submission.

## What changed

- Rebuilt the homepage around the approved hero, real fleet selection, booking guidance, practical benefits, FAQ, and final catalogue action. No invented reviews, fleet totals, insurance, payment, partnership, or support claims were added.
- Rebuilt vehicle browsing and details around uploaded photographs, LKR rates, specifications, and honest photo fallbacks. Desktop filters remain visible; phone filters use a dialog. Catalogue filters are retained in the hash route and restored after record navigation.
- Booking remains vehicle-first, followed by rental dates. The dialog shows billable days and the estimate, checks the existing availability endpoint, prevents repeated submission, handles conflicts, and displays the status returned by the API.
- Rebuilt the customer account around bookings, eligible cancellation, and retained history. Profile photo and password controls remain available; contact information remains explicitly read-only.
- Added a shared white operations shell and task-based navigation for inventory, vehicle forms and galleries, maintenance, reports, accounts, profiles, and notifications. Inventory and account tables become labelled record cards on phones.
- Record routes now carry `vehicleId` or `userId`. Records reload through existing services after refresh. Missing accounts and vehicles show recovery links instead of fabricated records. Customer sign-in returns to the selected vehicle through a restricted internal return destination.
- Centralised the presentation in design tokens and component, screen, layout, and customer styles. Removed the accumulated polish stylesheet. Added shared record loading, uploaded-image fallbacks, and confirmation dialogs with pending-action protection. Vehicle gallery previews are keyboard-operable.
- Maintenance hub filters now use recorded fleet locations. Network failures produce distinct errors rather than fabricated zero counts or empty histories.

No backend endpoint, database schema, deployment configuration, or Sprint 4 rental-lifecycle feature was changed.

## Verification

| Check | Result |
| --- | --- |
| Frontend tests | **122 passed**, no failures or skips; the 108-test baseline is retained |
| ESLint | Passed |
| Production build | Passed |
| Git diff whitespace check | Passed |
| Initial JavaScript, including static shared imports | **320,169 bytes**, below the 350,000-byte budget |
| Main JavaScript entry | 306,016 bytes; record/workspace pages retain route-level loading |
| Hero artwork | 44,728-byte WebP |

The 14 new regression tests cover record routes and missing accounts, obsolete requests, safe sign-in return navigation, catalogue filters and browser navigation, real uploaded images and fallbacks, billable-day estimates, availability failures and conflicts, duplicate booking/confirmation actions, accurate returned booking status, role gates, and LKR inventory pricing.

Browser inspection used **375px, 768px, and 1440px** viewports for public authentication and marketing screens, catalogue/details, customer bookings/profile/notifications, staff overview/inventory/forms/gallery/profile, maintenance, reports, and admin overview/directory/account forms and records. Final inspected screens had document scroll width equal to document client width. Table scrolling remains contained where appropriate. Populated screens, empty filter results and histories, missing records, and service-unavailable states were checked. The local APIs were temporarily stopped for outage checks and restored afterward.

Verified live local workflows include customer sign-in returning to a selected vehicle, booking creation, cancellation retaining history, vehicle/account record refresh, saving an existing admin account, maintenance creation and cancellation, and real image retrieval. Booking conflicts were also exercised against the local API. Browser keyboard checks verified dialog focus containment, Escape/return focus, and mobile workspace navigation containment. Automated tests cover password changes, forced-password behavior, validation, and role-restricted actions.

## Screenshots

These images contain clearly labelled local QA fixtures, not production fleet claims.

- [Desktop homepage](screenshots/landing-1440.jpg) and [complete homepage](screenshots/landing-full-1440.jpg)
- [Phone homepage](screenshots/landing-375.jpg)
- [Desktop catalogue](screenshots/browse-1440.jpg)
- [Phone booking dialog](screenshots/booking-dialog-375.jpg)
- [Phone inventory](screenshots/manage-fleet-375.jpg)
- [Desktop administration](screenshots/admin-dashboard-1440.jpg)
- [Phone user directory](screenshots/admin-users-375.jpg)

The complete local screenshot matrix, browser measurements, lint/test/build logs, and bundle measurement are in `../../verification/`. The pre-redesign source snapshot and patch are in `../../verification/frontend-before-redesign-20261013/`.

## Artwork attribution

The visual foundation and marketing vehicle artwork come from [Ravindu Dinushan's Car Rental template](https://ravindudinushan.github.io/Car-Rental/) and its [source repository](https://github.com/ravindudinushan/Car-Rental). The exact MIT license and copyright notice are preserved in [`src/frontend/public/licenses/car-rental-template.txt`](../src/frontend/public/licenses/car-rental-template.txt), which also ships in the production build. Actual catalogue and gallery photographs come from FleetFlow's existing image endpoints.

## Review boundaries

- Local QA data was created through the existing APIs: three vehicles labelled QA, a test customer, an upcoming booking, a cancelled booking, and a cancelled maintenance record. Template artwork uploaded to these QA vehicles is captioned as demonstration artwork. The private local fixture manifest is outside the repository in `../../verification/redesign-qa.json`; credentials are not included in this document or committed screenshots.
- Contact editing remains read-only because the current self-service contract does not support it. Password flows were tested automatically; live credentials were not changed through Browser.
- Inter loads from Google Fonts, with a system-font fallback. Browser inspection used the in-app browser and is not a full screen-reader/device certification or a production deployment check.
- Sprint 4 rental pickup, return, timeline, outbox, account revocation, reporting export, and deployment work remain separate stories. Merges and DevOps remain with the developer.
