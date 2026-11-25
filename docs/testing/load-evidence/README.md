# FleetFlow SE3112 - Aqqil's Load Testing handover

Assigned owner: **Aqqil M.R.M. (IT24104314)**. Prepared with Codex at Jadavan's request. Review the code and reproduce the results before presenting it.

## Start here

1. Extract this entire ZIP. Read `RESULTS.md` for measured outcomes.
2. Read `repository/docs/testing/load.md` for commands, acceptance criteria, the demo and ten VIVA answers.
3. Read `repository/docs/testing/system-test-environment.md` to start the real local database, APIs and frontend. Docker is optional; the portable Windows PostgreSQL procedure is included.
4. Open the raw evidence under `evidence/load/`. Evidence files are preserved byte for byte from the verified capture.

## Use the implementation

This package contains the testing contribution and shared setup files, **not the full FleetFlow application**. Use a FleetFlow clone with its existing backend/frontend source and dependency lockfiles. Requirements: .NET 10 SDK, Node 22, PowerShell, PostgreSQL, and k6 2.3.0.

The tested combined implementation is already on the published `test/load-e2e` branch at `e2edeb3`. If using that version, inspect the existing files instead of copying them again.

To integrate this contribution separately, start a personal branch such as `test/aqqil-load-handover` from the team's agreed develop version. Review and copy files inside `repository/` into the matching repository paths. Preserve any newer teammate edits. Commit the reviewed contribution under your own identity. The two handovers intentionally contain identical shared environment helpers; reconcile shared changes once when integrating both.

`ci/ase-load-reference.yml` contains only this role's job for review. The common action is under `repository/.github/actions/assessment-environment/`. Integrate with the existing workflow and its final quality gate; do not replace the full workflow with this fragment. The full combined branch already has this integration.

Set fresh synthetic `ASE_TEST_PASSWORD` and `ASE_DB_PASSWORD`, use a new `ASE_RUN_ID`, then start the environment and run the documented commands from the repository root. Keep the same environment values in the runner terminal. Runtime downloads, database data, node_modules and local credential files are deliberately excluded; install prerequisites as documented. Test output directories must be fresh.

## What is published and what is local

- Jadavan's mutation work was pushed to develop at `9694dfd`; its CI passed: https://github.com/IT24103433/Fleet_Flow/actions/runs/39401270485
- The load/E2E implementation was published separately at `e2edeb3`; all four testing jobs passed: https://github.com/IT24103433/Fleet_Flow/actions/runs/39403555100
- Final captured evidence and documentation commit `0784312d79aa29b56f5b04a7649cc2bac4ca03cd` remain local at Jadavan's request. No PR was created and this load/E2E contribution was not merged into develop in this work.
- Measurements were captured on 20 October 2026. Packaging does not constitute a new test run.

`SHA256SUMS.txt` covers the package's files. The environment document's final link is adapted to this independent handover; implementation and raw reports are unchanged.
