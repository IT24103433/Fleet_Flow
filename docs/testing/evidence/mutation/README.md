# Genuine mutation execution evidence

**Owner: Jadavan M., IT24103433. Captured 20 October 2026.**

- [Completed before run: HTML](2026-10-20/before/stryker/reports/mutation-report.html) / [JSON](2026-10-20/before/stryker/reports/mutation-report.json) / [metadata](2026-10-20/before/run.json)
- [Completed after run: HTML](2026-10-20/after/stryker/reports/mutation-report.html) / [JSON](2026-10-20/after/stryker/reports/mutation-report.json) / [metadata](2026-10-20/after/run.json)
- [Machine-readable before/after comparison](2026-10-20/comparison.json)
- [Final verification of the committed code: HTML](2026-10-20/final-verification/stryker/reports/mutation-report.html) / [metadata](2026-10-20/final-verification/run.json)
- [Independent original Identity baseline TRX](2026-10-20/unit-baselines/baseline-identity.trx)
- [Capture integrity manifest](2026-10-20/CAPTURE-SHA256SUMS.txt)
- [Analysis, commands and remaining gaps](../../mutation.md)

The before report is a **completed mutation execution that failed the configured score threshold**, with actual Stryker exit code 2 and score 67.68%. It is not described as a passing CI result. The after run exited 0 with score 95.96%. Its preflight passed 68 cases. Both runs have 99 valid mutants, zero timeouts, 11 scoped compile errors and 231 ignored scoped mutants. Killed increased from 67 to 95; surviving dropped from 28 to 2; uncovered dropped from 4 to 2.

Each capture preserves the generated HTML/JSON, real logs, preflight TRX, runtime information, effective configuration, pinned tool manifest, source hashes, metadata and summary. Base commit `db22b5e` was recorded along with `sourceHadUncommittedChanges=true`; the source was the original develop tree plus this local contribution, not a claimed clean checkout of that commit. Production source hashes match between runs. The after source-hash record includes the new mutation regression test file.

The raw JSON and HTML retain original absolute source paths and generator content. Do not edit their counts, statuses, source or timestamps. `CAPTURE-SHA256SUMS.txt` protects the captured file bytes; per-run `SHA256SUMS.txt` protects files within each original run. Local paths and original result-directory names in logs are historical execution details. They are not prerequisites for reproducing the run elsewhere.

This evidence demonstrates local execution only. A verified hosted GitHub Actions run URL must be added after pushing and observing the pipeline. Load and Playwright evidence are separate team deliverables.

After finalizing the runner, a third genuine execution verified clean committed source `1c5a43f` (`sourceHadUncommittedChanges=false` at execution start). It again passed 68 unmutated cases and produced 95 killed, 2 surviving, 2 uncovered and 0 timeout mutants: 95.96%, exit 0. Later handover commits preserve raw report bytes and add evidence links; production and test logic are unchanged from that verified source. The independent original Identity and Fleet TRX files are also retained under `unit-baselines/`.
