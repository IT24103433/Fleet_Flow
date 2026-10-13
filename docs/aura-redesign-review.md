# FleetFlow Aura desktop redesign

Implemented locally on `feat/frontend-professional-polish` on 13 October 2026. The supplied Aura markdown overrides the earlier blue/Inter direction. This review supersedes the visual direction in `frontend-professional-polish.md`; its functional fixes remain in place.

## Design and behaviour

White surfaces, purple `#5B1E91`, lavender `#BDB5E9`, charcoal text, red destructive/error feedback, Plus Jakarta Sans, and JetBrains Mono form the shared theme. Panels use 32px radii and controls use 24px radii. Marketing sections retain 80px spacing. Customer screens, booking dialogs, profiles, fleet inventory, maintenance, reporting, notifications and administration use the same tokens.

The centred two-line hero leads into an asymmetric vehicle showcase with three draggable workflow panels. Dragging is confined to that canvas and changes presentation only. Handles support arrow keys, Shift for larger steps, Home/Escape to reset a panel, and a labelled reset-all button. Bounds are recalculated on resize; positions reset on remount/reload. Other forms, tables and dashboard content keep their established arrangement.

GSAP and `@gsap/react` provide scoped entrances, magnetic CTA feedback, restrained drag tilt, and a short pinned booking guide. The guide keeps its text fully opaque throughout the animation and recalculates its trigger after real fleet data and fonts load. Reduced-motion users receive visible static marketing content and no magnetic pull or tilt. Route changes revert the page's animation context and disconnect its resize observers. Marketing motion lives in the lazy homepage route; other page routes remain lazy-loaded.

Phosphor named icon imports replace the mixed SVGs and interface arrow glyphs, including pagination. `src/frontend/AGENTS.md` records regular weight, decorative-icon hiding, labels for icon-only controls and named per-icon imports.

The existing services, callbacks, hash routes, `vehicleId`/`userId` context, restricted customer sign-in returns, booking estimates, availability checks, conflict handling, duplicate-submission protection and role/forced-password restrictions remain. No backend endpoints, schemas or Sprint 4 rental-lifecycle features were added.

## Component provenance

- Magnetic Button: [21st.dev listing](https://mcp.21st.dev/@mohammadshehadeh/components/magnetic-button), [Hirael author source](https://hirael.com/r/magnetic-button.json), Mohammad Shehadeh. Adapted pointer-centre feedback to GSAP with a stable hit area. Exact MIT notice retained in `src/frontend/public/licenses/hirael-mit.txt`.
- Draggable Card: [Aceternity author source](https://ui.aceternity.com/registry/draggable-card.json), Manu Arora. Adapted the tilt/drag interaction to GSAP, container bounds, keyboard controls and reset. The [Aceternity licence](https://ui.aceternity.com/licence) permits application end-product use; it is not MIT. Attribution and applicable terms are recorded in `src/frontend/public/licenses/aceternity.txt`.
- Marketing vehicle artwork: the previous Car-Rental template asset and its MIT/copyright notice remain. Listings continue to use photographs returned by the vehicle image service, with honest missing-photo states. Local QA vehicles are explicitly labelled demonstration fixtures.

Original component source downloads were cached under `Z:\Programs\FleetFlowCaches\Templates\Aura`. No paid generation services or additional animation frameworks were used.

## Verification

- Full frontend suite: **126 passed** — all 122 existing tests plus four focused Aura tests. New coverage exercises all-edge bounds, keyboard steps/reset, rendered pointer bounds, resize-observer cleanup, reduced-motion marketing visibility and magnetic suppression. The sign-in regression waits for its lazy route to finish loading before typing.
- Lint, production build and `git diff --check`: passed. Compatible tooling patches cleared the three inherited dependency advisories; npm reported zero vulnerabilities after those updates.
- Initial entry/static-import JavaScript: approximately **299 KB uncompressed**, below the existing 350 KB entry budget. The homepage adds a separate approximately **138 KB** lazy route containing GSAP/ScrollTrigger. This budget measures the entry and static imports, not all files downloaded when visiting the homepage. The previous entry was 320,169 bytes.
- One desktop Browser review at **1440 × 900** covered the homepage, bounded pointer dragging, keyboard/reset, catalogue, sign-in return to the selected vehicle, booking dialog (three days and LKR 54,000 estimate), existing confirmed/cancelled bookings, manager inventory, maintenance scheduling form, reports, profile controls, admin directory and account refresh. Reviewed page layouts had no page-wide horizontal overflow; the final preview logged no console errors.
- No additional bookings, maintenance writes, destructive admin operations or password changes were submitted during this walkthrough. Existing mutation contracts remain covered by the regression suite and prior functional baseline verification.
- Dedicated mobile/tablet testing and optimisation were skipped as requested. Existing responsive rules remain, with a basic narrow-layout fallback for the new showcase. Deployment and production-data verification remain the developer's DevOps work.

Desktop captures are in `docs/screenshots/aura-*-1440.jpg`, including the homepage, catalogue, booking dialog, customer account, staff workspace, inventory, maintenance, reports and administration.

## Verified backup and rollback

Before any Aura frontend edit, 123 files were archived, including uncommitted/untracked frontend source, artwork, package/configuration files, licences and existing review screenshots. Each file was extracted into a temporary verification directory and checked against its SHA-256 manifest.

Archive:
`Z:\Programs\FleetFlowCaches\FrontendBackups\frontend-before-aura-20261013-120717-934b6e.zip`

Archive SHA-256:
`76129ca8f4bb8beac7f289d1222ac4f48bc00ddb9b7b53713ca0f9b5855a8fc7`

The adjacent receipt records the archive hash and the specific files introduced by this redesign. Dependency/build/cache folders were excluded; environment secrets remain in place. The restore helper revalidates the archive and per-file hashes, checks the repository and every target path, creates and verifies a safety backup of the current version, then restores the previous files and removes only those recorded Aura additions. Files added later for unrelated work are preserved. Backend code, database contents and environment files are outside the restore scope.

From the ChatGPT project workspace (the parent of `Fleet_Flow`), stop the frontend dev server before an actual restore, then run:

```powershell
# Preview the validated restore and exact Aura file removals.
.\verification\Restore-FrontendBackup.ps1 -DryRun

# Restore the prior frontend; a verified safety backup is created first.
.\verification\Restore-FrontendBackup.ps1

# Reconcile the excluded dependencies with the restored lock file.
Set-Location .\Fleet_Flow\src\frontend
npm.cmd ci
npm.cmd run dev -- --host 127.0.0.1
```

The helper uses `verification/frontend_backup.py` and the bundled Python runtime. The live restore dry run passed. A disposable rollback simulation also passed: it restored old files, verified a safety snapshot, removed only recorded Aura additions, preserved later unrelated files/environment/backend sentinels, and rejected unsafe targets. The live frontend was not rolled back. All changes remain local for review and the developer's PR submission, merges and DevOps.
