# Verified unit-testing evidence

**FleetFlow Automations | Group 27 | Sanjeevsaran J., IT24104155**

The execution evidence below was collected on 20 October 2026 against clean source commit **`5599029d1b460679c546c01c012393971db400f4`**. This later evidence commit adds documentation and captured files; it does not change the tested production or test logic.

## Recorded outcomes

| Execution | Identity cases | Fleet cases | Total passed | Failed / skipped | Evidence |
| --- | ---: | ---: | ---: | --- | --- |
| New Sanjeevsaran suites, `Category=ASE.Unit` | 30 | 60 | **90** | 0 / 0 | [Focused report](2026-10-20/focused/index.html), [JSON](2026-10-20/focused/summary.json), [metadata](2026-10-20/focused/run.json) |
| Existing unit cases plus the new suites | 163 | 129 | **292** | 0 / 0 | [Regression report](2026-10-20/regression/index.html), [JSON](2026-10-20/regression/summary.json), [metadata](2026-10-20/regression/run.json) |
| Reporting-tool acceptance checks, Python stdlib | n/a | n/a | **9** | 0 / 0 | [Actual console output](2026-10-20/report-tool-tests.log) |

The 90 new cases are included in the 292-case regression. These are not 382 distinct .NET cases. The nine Python cases validate reporting safeguards and are separate from xUnit totals.

Both final .NET executions used the official .NET SDK container, SDK **10.0.402**, runtime **10.0.13**, Ubuntu 24.04, Linux x64. Docker networking was disabled, source and cached packages were read-only, and no application API/database/broker was started for these runs. SDK information and actual output are in each `execution.log`. Both summaries have `accepted: true`, process exit code zero and `sourceHadUncommittedChanges: false`.

The regression filter intentionally excludes the single pre-existing `OnboardingUniquenessTests` PostgreSQL integration case. That case remains unchanged and belongs to ordinary backend integration CI. No claim is made that the complete solution or deployed system was retested by this unit contribution.

## Measured coverage

Coverlet measured five full production types, including their methods outside the selected signing/controller paths. Percentages below do not describe the whole application.

| Scope | Covered lines / measured lines | Covered branches / measured branches |
| --- | --- | --- |
| New 90 cases only | 185 / 262, **70.61%** | 105 / 190, **55.26%** |
| New plus existing unit cases | 210 / 262, **80.15%** | 126 / 190, **66.32%** |

`MaintenanceStaffDirectory` has 18/18 measured lines and 22/22 measured branches covered by the new suites. `ReportCsv` has 32/32 lines and 28/30 branches. `AuthenticationService` includes database-dependent methods outside `CreateSession`, which remain honestly included in its denominator. A type with zero measured branches is shown as not applicable, not 100%.

Raw TRX, Coverlet JSON/Cobertura XML and logs are retained inside each run folder. VSTest produced identical copied attachments under `In/<container>` as well as the original collector directory. The summarizer unions source locations and rejects conflicting reports; copied attachments do not inflate coverage.

## Reproduce and inspect

Use [the setup and commands](../../unit.md). Open either HTML report locally. Each report links to its raw files and shows individual test outcomes. The [focused screenshot](2026-10-20/focused-report.png) is a browser capture of the generated report, not a substituted test result.

Per-run `SHA256SUMS.txt` files cover the raw outputs and generated report. The top-level [capture manifest](2026-10-20/CAPTURE-SHA256SUMS.txt) also covers screenshots, helper-test output and the source manifest. Verify hashes before presenting copied evidence. Hashes detect alteration; they do not authenticate the environment independently.

`source-files.json` records SHA-256 hashes of the implementation commit's changed files as present during capture, alongside the tested commit and starting baseline. Source control provides the complete tree and diff.

## CI and operational status

The existing CI workflow now contains the unit job and its artifact upload; this branch has not been pushed in this task, so there is **no new hosted CI run claimed here**. The team should retain its run URL after publishing the branch. No Azure deployment or production data was changed.

During development, strict mocked-handler disposal and shell CRLF handling were corrected before final execution. A separate Windows regression attempt was blocked by Application Control when loading instrumented assemblies; it was not accepted as passing. The final accepted evidence uses offline Docker. Failed preliminary runs remain in local ignored `artifacts/` folders and are not mixed into the final passing bundle.

The [demo and VIVA guide](../../unit-demo.md) explains what this evidence proves and the integration/E2E responsibilities that remain outside this unit layer.
