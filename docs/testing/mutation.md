# Jadavan: Mutation Testing and CI Coordination

**SE3112 | Group 27 | Jadavan M. | IT24103433**

## What was implemented

Stryker.NET 5.0.0 is pinned in the repository-local `dotnet-tools.json` manifest. The existing Vehicle test project is reused to mutate two real Fleet production files:

- `Services/MaintenanceStaffDirectory.cs`: authenticated upstream responses, status handling, account validation, list limits and cancellation.
- `Services/ReportCsv.cs`: export schemas, both maintenance row sources, CSV safety, limits and date conversion.

The configuration is `tests/FleetFlow.VehicleService.Tests/stryker-config.json`. Standard mutations and per-test coverage analysis are used, with two workers. Generated code, migrations, application startup and other service files are outside the selected scope. No production code or Sanjeevsaran-owned tests were changed. Eight new discovered cases are isolated in `AseMutation/MutationRegressionTests.cs`, tagged `Category=ASE.Mutation` and `Owner=Jadavan`.

## Actual measured result

Both runs completed on 20 October 2026 using .NET SDK 10.0.402 and the same Stryker version, production source, mutation level, scope, concurrency and thresholds. The deliberate difference is the selected test suite: `Category=ASE.Unit` before; `Category=ASE.Unit|Category=ASE.Mutation` after. The comparison validates identical mutant fingerprints and identical checked-in configuration/tool manifests. Raw report IDs may change in future runs; source location, operator and replacement identify the behavior.

| Measure | Before | After |
| --- | ---: | ---: |
| Passing unmutated Fleet cases | 60 | 68 |
| Killed mutants | 67 | 95 |
| Survived mutants | 28 | 2 |
| NoCoverage mutants | 4 | 2 |
| Timeout mutants | 0 | 0 |
| CompileError mutants in the scoped JSON | 11 | 11 |
| Ignored mutants in the scoped JSON | 231 | 231 |
| Valid score denominator | 99 | 99 |
| Mutation score | 67.68% | 95.96% |
| Stryker exit code | 2: below threshold | 0 |

This is an improvement of 28.28 percentage points: 26 surviving mutants and two uncovered mutants became killed. The original focused Identity suite also passed independently: 30 cases. These measurements describe the selected two-file scope, not the entire FleetFlow application.

The checked-in execution captures are linked from [the evidence index](evidence/mutation/README.md). Each run records a base commit and an honest dirty-tree flag: execution happened on `develop` commit `db22b5e` plus the local contribution. `source-files.json` includes source hashes, including the new untracked regression file in the after run. The JSON and HTML reports retain the original generator output; captured absolute paths identify the execution machine, not a required checkout location.

## Defects the original tests could miss

1. **HTTP status rejection removed (staff-directory line 23).** Original error responses had empty bodies. With the explicit HTTP guard removed, JSON parsing still failed and produced the expected exception, so the mutation survived. New tests return valid staff JSON with 401, 403, 429, 500 and 503 statuses. Without the guard, a valid-looking but ineligible account would be returned. All five cases still require rejection. The mutant is now killed.
2. **CSV column names erased.** Partial assertions checked selected header fragments. A complete ordered booking-schema assertion now detects missing column names even for an empty export.
3. **Maintenance activity rows dropped.** Existing row-limit tests populated only the vehicle source. A mixed-source export now asserts the full schema and both complete rows. It detects an empty activity row and `Concat` changed to `Except`, along with missing labels and headers.
4. **Local timestamps relabelled as UTC.** UTC and Unspecified inputs alone did not distinguish conversion from relabelling. A Local input now checks the UTC instant. The machine used Sri Lanka Standard Time; the Linux mutation CI job sets `TZ=Asia/Colombo` so a non-zero local offset exercises that distinction. The test remains correct in other time zones, but this mutant could survive on a UTC-only host.

## Remaining mutants and limitations

- Two **surviving string mutants**, at ReportCsv lines 17 and 19, remove exception-message text. Existing tests intentionally verify exception type and failure behavior. Diagnostic prose is not an established public contract; these are not declared equivalent mutants, and they remain in the denominator. Confirm message requirements with the team before adding brittle exact-message assertions.
- Two **NoCoverage string mutants**, at lines 45 and 46, alter the empty-string fallback for `IFormattable.ToString` or an object's `ToString` returning null. Ordinary export DTO values do not exercise those unusual custom-object paths. They remain explicit gaps; no result is hidden or reclassified.
- Eleven scoped mutants could not compile. They cannot represent executable faults and are excluded from the valid score denominator, with the tool's status retained. Stryker also logs mutation-generation compile problems elsewhere before scope filtering; do not confuse the initial whole-project log counts with the final scoped JSON counts.
- Ignored mutants are Stryker's filtered/structural statuses, not proof of correctness. No ignore annotation or mutation-family exclusion was added to raise the score.
- There were zero timeouts. A timeout would be detected for scoring, but would require review for infrastructure slowness or a true non-terminating mutant.
- Mutation testing measures whether this suite detects generated faults. It does not prove security, correctness, load capacity, database behavior or E2E functionality. Higher coverage and higher mutation scores are related but different claims.

### Score formula

`100 * (Killed + Timeout) / (Killed + Timeout + Survived + NoCoverage)`

After: `100 * 95 / 99 = 95.96%`. CompileError and Ignored are excluded. NoCoverage stays in the denominator. The quality gate uses a **70% break threshold**, selected before the first completed run as an initial team floor; the before run failed it and the after run passed without lowering it. High=90 and low=70 provide report colors. This is a project choice, not a claimed SE3112 requirement. The team can tighten it after reviewing acceptable gaps.

## Reproduce from the repository root

Prerequisites: .NET SDK 10.x, PowerShell (PowerShell 7 on Linux), Python 3 and internet access for the initial NuGet/tool restore. No API server, database, Kafka broker or Docker daemon is required. Test responses and credentials are synthetic and remain in process.

```powershell
dotnet tool restore

# Windows: use python, or an absolute Python executable path.
./tools/testing/Run-Mutation.ps1 -PythonExecutable python -ResultsDirectory artifacts/mutation-demo

# Linux / GitHub Actions:
./tools/testing/Run-Mutation.ps1 -PythonExecutable python3 -ResultsDirectory artifacts/mutation-ci

# Recreate the original unit-only comparison even after the extra tests exist.
./tools/testing/Run-Mutation.ps1 -Baseline -PythonExecutable python -ResultsDirectory artifacts/mutation-baseline
```

Each results path must be new; the runner refuses to overwrite evidence. The baseline is expected to exit non-zero below the unchanged 70% floor. Inspect its completed report and actual error before proceeding; do not relabel an installation/build/test failure as a valid baseline. The early development attempt in `artifacts/mutation/before` failed on a CLI option and has no score; it is not the completed baseline capture.

```powershell
python tools/testing/summarise_mutation.py --before artifacts/mutation-baseline --after artifacts/mutation-demo --output artifacts/mutation-comparison.json
python -B -m unittest discover -s tools/testing -p test_mutation_summary.py -v
```

The runner first restores the pinned tool and checks the selected unmutated tests, then runs Stryker. Stryker 5.0.0 accepts the test-case filter through the configuration file rather than the CLI option used by some other releases, so the runner saves an `effective-config.json` for that run. `--break-on-initial-test-failure` prevents accepting an invalid initial run.

Outputs: `run.json`, base and effective configuration, tool manifest, runtime information, source hashes, preflight TRX/log, Stryker HTML/JSON report, execution log, `summary.json`, `summary.md` and `SHA256SUMS.txt`. A non-zero Stryker status is preserved even when a readable report exists. The summarizer rejects absent/duplicate reports, unknown/incomplete statuses, empty mutation sets and empty/partially passing preflight tests. Seven mutation-evidence tests plus the nine existing unit-evidence tests passed locally.

## Shared pipeline coordination

The existing `.github/workflows/ci.yml` now contains `ase-mutation`, dependent on the existing `ase-unit` job. It runs on a fresh Ubuntu runner with .NET 10, validates evidence rules, executes the actual mutation runner and publishes `ase-mutation-evidence` even after failure. There is a 25-minute job limit. The final quality gate depends on mutation too and explicitly fails on failed, cancelled or skipped dependencies. Deployment workflows are unchanged.

The supplied workflow has been parsed and its dependencies checked locally. **No GitHub Actions execution is claimed until this branch is pushed and a real run is recorded.** The k6 and Playwright implementations were absent from the inspected `develop` baseline and are still owned by Aqqil and Varun. Their required handover is documented in [pipeline-integration.md](pipeline-integration.md). There are no invented passing placeholder jobs.

See [mutation-demo.md](mutation-demo.md) for the two-minute demo and fifteen VIVA questions.

## Tool documentation

- [Stryker configuration and thresholds](https://stryker-mutator.io/docs/stryker-net/configuration/)
- [Stryker reporters](https://stryker-mutator.io/docs/stryker-net/reporters/)

Installed-tool help and genuine execution were used to verify the pinned version's supported options.
