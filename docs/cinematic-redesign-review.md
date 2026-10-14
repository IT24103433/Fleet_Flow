# Approved luxury frontend baseline — 13 October 2026

This is the current, user-approved frontend. The final luxury design and validation are described first; the earlier cinematic review below is retained as historical backup, image-attribution and local QA evidence.

The PR preserves the existing Identity and Fleet API contracts. Backend source, infrastructure, environment secrets, uploaded user data and local QA database changes are not included. The current homepage, staff workspace, administration and report screenshots are under `docs/screenshots/luxury-*`.

The exact approved frontend was archived before PR preparation: 196 files, extracted and SHA-256 verified. Archive: `Z:\Programs\FleetFlowCaches\FrontendBackups\frontend-before-aura-20261013-194520-cb82e3.zip`. SHA-256: `37ab685f8f5838ae4ed1e664c6ff92a7b08316f24485956951ee409a933ffd0b`. Previous backups and guarded restore helpers remain preserved.

## Final implementation


This revision supersedes the previous pale cinematic homepage. The supplied Opurent and Redline recordings were decoded into contact sheets and inspected for full-width photography, dark automotive art direction, strong typography and alternating visual chapters. Their paid artwork and runtime were not copied.

The homepage now uses original graphite-coupe campaign artwork, a dark header, ivory fleet sections, deep violet accents, two clear hero actions, aligned booking steps and a dark benefits chapter. The Corolla hatchback is removed from the visible hero. Real listing images, LKR prices, booking contracts and role routing are retained. The hero is AI-generated marketing artwork, explicitly identified in Credits; it is not a claim about the rental fleet.

The marketing hero uses GSAP scroll progress to control camera scale, horizontal movement and a restrained perspective effect. A short marketing pin holds the composition while the image moves; copy and actions remain immediately usable. Sections enter with 80ms staggering. Reduced motion creates no marketing triggers. Route cleanup removes the pin and late image listeners. These are animated image treatments, not a video or an interactive 3D vehicle model.

The original image was generated with the built-in imagegen tool and retained under `Z:\Programs\FleetFlowCaches\PhotographyOriginals\luxury-coupe-campaign-original.png`. Repository-packaged WebP/AVIF variants need no Z-drive access. At 1440px, the AVIF is 39,981 bytes; the 1920px AVIF is 55,484 bytes. The full prompt and asset hashes are in `src/frontend/public/assets/photography/luxury-artwork.json`.

Components use reusable author source: Magic UI Shimmer Button for the primary marketing CTA; Magic UI Bento Grid for booking guidance and operational panels; Aceternity Container Scroll progress adapted to GSAP. Sources: [Shimmer Button](https://magicui.design/docs/components/shimmer-button), [Bento Grid](https://magicui.design/docs/components/bento-grid), [Container Scroll](https://ui.aceternity.com/components/container-scroll-animation). Copyright, licences and adaptation details are retained in public Credits. Operational buttons keep their existing semantics, callbacks and submission guards, with compact 8px corners.

The staff shell has a dark sidebar with a separately scrolling navigation area and a fixed account footer. Logout is visible at the top right and returns to staff sign-in. Account photographs remain isolated by account. Fleet readiness visualises actual available, in-use, maintenance and retired counts. Administration also presents booking-status analysis from the existing operational-statistics endpoint. Fleet reports show both distributions. No fabricated time-series, revenue, backend, database, Sprint 4 or Azure changes were introduced.

## Backup and rollback

The immediately preceding frontend is preserved in `Z:\Programs\FleetFlowCaches\FrontendBackups\frontend-before-aura-20261013-180111-5d0492.zip` (historical helper naming). All 178 files were extracted and hash-verified before edits. Archive SHA-256: `db5bb8a3bc83ab807b3b8943ac42753bb0be4b0b8dcf0fc46f96f19a7532f922`.

Run `verification\Rollback-LuxuryFrontend.ps1 -DryRun` from the project mirror to preview restoration. Run it without `-DryRun` to restore. It checks archive and file hashes, validates the exact repository and frontend paths, takes a safety backup first, and removes only files recorded as introduced by this revision. Environment secrets and backend files are excluded. Earlier backups and QA-data rollback instructions remain preserved.

## Acceptance for this revision

Desktop review only, at 1440 × 900: hero asset loading and scroll transform, navigation cleanup, staff chart and top-right logout, admin/report charts and accessible sidebar. Screenshots are under `docs/screenshots/luxury-*`. No mobile/tablet optimisation or review was performed.

Regression: all 126 existing tests pass, including booking conflicts, duplicate submission prevention, record navigation, role permissions, account photographs, maintenance and report contracts, reduced motion and cleanup. Lint and production build pass after one chart immutability correction. `git diff --check` passes. Exact bundle totals and guarded rollback dry-run evidence are in the project mirror's `verification/luxury-bundle.json` and `verification/luxury-restore-dry-run.json`. Non-failing Vite HMR-port and React test-act warnings remain in the existing test harness.

Entry JavaScript is **302,498 uncompressed bytes**, below the 350,000-byte budget. The lazy homepage module is 131,010 bytes; its additional static dependency closure is 142,376 bytes in total. Marketing motion remains isolated from the initial entry.

Prepared on `feat/frontend-professional-polish` for a pull request into `develop`. Merge the manual release-gate PR first. Sprint 4 backend work, Azure deployment, and live-data migrations are outside this frontend PR.


---

## Historical cinematic review — superseded

Implemented locally on `feat/frontend-professional-polish`. No backend source, schema, Sprint 4 implementation, Azure fleet, PR, push, merge or deployment changes.

## Design acceptance

Desktop Browser review used a 1440 × 900 viewport. The customer homepage now has a two-line editorial headline, two stationary actions, one framed photograph, three aligned booking steps, varied real fleet cards, practical benefits, FAQ and final action. The white/purple/lavender palette and Plus Jakarta Sans / JetBrains Mono remain.

Draggable panels, handles, resets, oversized lettering, magnetic displacement and the pinned guide are removed. Hero media reveals over 750 ms after its own image loads. Marketing entrances stagger by 80 ms. Essential copy never fades out; forms, filters, operational actions and dialogs remain stationary. Reduced motion renders the complete static layout. Route cleanup removes load listeners, reverts tweens and removes ScrollTriggers.

Authentication uses an original route-line SVG. Shared account presentation resolves the presented account's own photo, shows its full name with username fallback, and uses a neutral Phosphor User icon for missing/failed images. Changing the account or photo resets failure state. Title-case role labels stay on one line. Seeded uppercase full names are actual API data and were preserved.

Staff/admin headers provide context without duplicating page headings. Dashboards show real metrics, attention items and compact task links. Inventory uses uploaded thumbnails. Notification counts, missing icon containers, action sizes, report empty states and wrapped status labels were corrected.

Reviewed visitor, authentication, booking, customer dashboard, staff dashboard, inventory, maintenance/history, reports, profiles, admin directory and refreshed account records. No page-wide horizontal overflow was found. Isolated read-only fixtures checked slow fleet/photo metadata loading, empty results, failed requests/photos and reduced motion; fixture sources are retained outside the repository in `../verification/cinematic-state-fixtures`. Production web fonts loaded successfully; system fallback fonts remained readable in the fixtures. The fixtures are removed from the delivered frontend.

Screenshots are in `docs/screenshots/cinematic-*`. Browser capture scales some 1440 × 900 viewport captures to 1425 × 891 JPEGs. The populated staff attention screenshot records the local QA work order before its later completion.

## Regression acceptance

- **126/126 frontend tests passed:** all 122 functional tests retained, with only four obsolete drag tests replaced by account-photo isolation, failure/reset, name/role and motion/loading/cleanup coverage.
- Lint, production build and `git diff --check` passed after the implementation and review fixes.
- Browser sign-in returned to the selected Corolla; its two-day estimate was LKR 18,000, booking returned Confirmed, and cancellation retained history. Account record IDs survived refresh; Browser Back returned to the directory; missing records showed recovery rather than fabricated accounts.
- Local API checks verified overlap HTTP 409, customer maintenance denial HTTP 403, maintenance update/start/completion/history/availability release, and fleet-summary, operational-statistics, maintenance and booking report contracts.
- Password, forced-password, ownership, confirmation and submission-guard regressions remain covered by the existing suite. No real credentials or account permissions were changed during Browser review.

Entry JavaScript, including static imports and measured uncompressed with the existing definition: **302,368 bytes / 350,000**. The lazy homepage chunk is **128,391 bytes**; its additional static dependencies bring the separately loaded homepage graph to **139,310 bytes**, excluding the already-counted entry. Evidence: `../verification/cinematic-bundle.json`, `cinematic-homepage-bundle.json`, `cinematic-tests.log`, `cinematic-lint.log`, `cinematic-build.log` and `cinematic-live-regression.json`.

Dedicated mobile/tablet review and optimisation were skipped as requested. Existing responsive rules remain. Azure/deployment verification and Sprint 4 lifecycle features remain separate work.

## Photography and attribution

Runtime hero variants are packaged under `src/frontend/public/assets/photography`; no runtime URL references Z. Original photographs remain under `Z:\Programs\FleetFlowCaches\Templates\Cinematic`.

| Use | Creator / licence | Preparation |
| --- | --- | --- |
| Corolla **hatchback**, marketing only | ae_works, CC BY 2.0 | 960/1440/1920px WebP and AVIF, original aspect retained, CSS display crop |
| Corolla Hybrid **Saloon**, local sedan listing | Vauxford, CC BY-SA 4.0 | 1600 × 928 WebP, 166,092 bytes |
| Mazda CX-5 Diesel, local SUV listing | Vauxford, CC BY-SA 4.0 | 1600 × 825 WebP, 168,348 bytes |

The 1440px hero AVIF is 174,707 bytes; its WebP fallback is 287,500 bytes. Sources, creator links, licence links, dimensions, original hashes and modifications are provided in `/credits.html` and `/assets/photography/credits.json`. Adapted listing photographs retain CC BY-SA 4.0. Listing uploads use the existing WebP-capable API. Earlier MIT artwork/notices remain; unused Aura component notices are preserved. The supplied Framer archive informed composition only; no compiled runtime or branded assets were copied.

## Verified backups

All archives remain under `Z:\Programs\FleetFlowCaches\FrontendBackups`.

| Snapshot | Verification |
| --- | --- |
| Earlier 94-file folder, `../verification/frontend-before-redesign-20261013` | Preserved; historical manifest and `index.html` missing. **Not a complete verified backup.** |
| Pre-Aura ZIP `frontend-before-aura-20261013-120717-934b6e.zip` | Archive SHA-256 and all 123 file hashes reverified. SHA: `76129ca8f4bb8beac7f289d1222ac4f48bc00ddb9b7b53713ca0f9b5855a8fc7` |
| Pre-cinematic ZIP `frontend-before-aura-20261013-151804-89943e.zip` | Complete current Aura frontend, configuration, licences and screenshots; 146 files extracted and hash-verified. SHA: `87349971f0dc536286857f296bc31c8fa0b7489b789fd43e14926d6cc7ee530e` |

Dependency/build folders and environment secrets were excluded. The new archive retains the helper's historical `before-aura` filename; its contents are the **pre-cinematic Aura version**.

Database backups: `Z:\Programs\FleetFlowCaches\DatabaseBackups\cinematic_20261013_153623`. Both dumps were restored into new isolated verification databases; all table fingerprints, foreign-key restoration and affected vehicle relationships matched. Verification databases were removed afterward; working databases were never restored over.

- `fleetflow_fleet.dump`: `bd8927044de02337ff0a49a6991be78a18d0f8e6567b5fd4b3790ce6ff35838f`
- `fleetflow_auth.dump`: `60b24ef605a2dbcf662f34b117c26543421e5b1f895727a4a11ce7e6e53ae3e7`
- UTF-8 affected-record export and hashes are beside the dumps. An earlier export had Windows locale decoding artefacts; its backup was preserved, and the corrected dumps/export were verified before fleet writes.

## Separate local QA phase and rollback

Only fixed loopback APIs (`127.0.0.1:5001/5002`) and `fleetflow_postgres` bound to `127.0.0.1:5432` were used. The refresh was rerun successfully without duplicate creation.

| Record | ID | VIN / plate | Result |
| --- | --- | --- | --- |
| Booked Lamborghini | `75392afe-c153-4033-8f7f-b8f7b8cf30d5` | `JTDBR32E660713901` / `QA-6607139-0` | Entire vehicle and booking/maintenance/image records unchanged |
| Touring duplicate | `0997a10c-ddee-45c9-92b9-368f14499d09` | `JTDBR32E660713911` / `QA-6607139-1` | Available → Retired; cancelled maintenance and images retained |
| City duplicate | `53eda5a3-cfcd-4912-b8e3-ecf767db1840` | `JTDBR32E660713921` / `QA-6607139-2` | Available → Retired; images retained |
| 2019 Corolla Hybrid Sedan QA | `cadeae00-9830-4d3e-839a-c338de3ad3b8` | `JTDBR32E190000001` / `QA-COR-2019` | Existing sedan category, Hybrid/Automatic/5 seats, illustrative LKR 9,000/day |
| 2019 Mazda CX-5 Diesel QA | `542e02f3-129e-4d4c-af35-f9fb0be0a99d` | `JM3KF4BD190000002` / `QA-MAZ-2019` | Existing SUV category, Diesel/Automatic/5 seats, illustrative LKR 14,000/day |

Regression-created history is retained: two cancelled Corolla bookings and one completed Mazda maintenance record, explicitly local QA. Neither new vehicle has active QA bookings or maintenance after verification. The existing Lamborghini booking remains confirmed.

**Frontend rollback**, run from the project workspace:

```powershell
.\verification\Rollback-CinematicFrontend.ps1 -DryRun
.\verification\Rollback-CinematicFrontend.ps1
```

The helper verifies archive/content hashes and resolved targets, creates and verifies a fresh safety snapshot, restores the 146 baseline files, and removes only the recorded files introduced by this revision. Permitted targets are this repository's `src/frontend`, `docs/screenshots`, and the three named frontend review notes. Environment files, backend files, data and unrelated later files remain. A disposable restore simulation verified these protections. To subsequently return to the older blue version, the preserved `Restore-FrontendBackup.ps1` selects the verified 123-file pre-Aura ZIP.

**QA-data rollback**, independent of frontend rollback:

```powershell
& 'Z:\Programs\node.exe' .\verification\rollback-cinematic-qa.mjs
& 'Z:\Programs\node.exe' .\verification\rollback-cinematic-qa.mjs --apply
```

Default is a dry run. Apply creates and isolated-restore-verifies fresh safety dumps of both local databases first. Its only write targets are the four manifest-listed QA vehicle statuses: restore the two duplicates to Available and retire eligible demo records. Identity/status drift or subsequent active bookings/maintenance cause a skip. The booked Lamborghini is always excluded. Atomic status updates take the same vehicle row lock used by existing booking/maintenance writes and recheck active relationships inside the transaction; this avoids the guard-to-write race. This is a local repair, without Kafka publication. It never deletes records, images or history and never restores old dumps over live data.

The rollback SQL was exercised in an isolated database restored from the verified dump. Active bookings and maintenance blocked updates; cancelled history remained; working records stayed unchanged. Evidence: `../verification/cinematic-qa-rollback-verification.json`, `cinematic-qa-rollback-dry-run.json`, `cinematic-backup-evidence.json` and `cinematic-restore-dry-run.json`. **No working frontend or QA-data rollback was performed.**
