# Sanjeevsaran: Unit Testing and Mocking

**SE3112 | Group 27 | Sanjeevsaran J. | IT24104155**

## What this contribution demonstrates

The new `AseUnit` folders add 90 independently discovered xUnit cases to the existing Identity and Vehicle test projects. `[Theory]` expands both inline inputs and strongly typed `TheoryData` supplied through `[MemberData]`. Production business code is unchanged. Existing projects and Moq are reused rather than creating a parallel test application.

| Suite | Real subject | Test double and risks checked |
| --- | --- | --- |
| `AuthenticationBoundaryTests` | `AuthController` | Strict registration/authentication/photo services; exact delegation, 401/403/409/500 mappings, sanitized failures, missing ownership, password renewal order and forbidden calls |
| `SessionSigningTests` | `AuthenticationService.CreateSession` | Providerless EF context and strict password hasher; real JWT signature validation, issuer/audience, role/access-version/forced-password claims, UTF-8 key-length boundaries, expiry fallback |
| `MaintenanceStaffDirectoryTests` | `MaintenanceStaffDirectory` with real `HttpClient` | Strict protected `HttpMessageHandler.SendAsync` and service-token provider; exact path/Bearer token, 0/1/1000 and 1001 accounts, malformed/duplicate identities, HTTP failures, timeout versus caller cancellation |
| `ReportCsvSafetyTests` | `ReportCsv` | Mostly pure functions; strict counted-list doubles for overflow; formula injection, Unicode prefixes, quoting/newlines, decimal culture, UTC, 9,999/10,000/10,001 rows, exact exported status/amount/IDs |
| `ReportBoundaryTests` | `ReportsController` | Strict reporting service; argument/outage mapping, unavailable metrics, CSV bytes/name/type, cancellation and no fabricated successful export |

The new cases are tagged `Category=ASE.Unit` and `Owner=Sanjeevsaran`. They use synthetic names, IDs, credentials, signing keys and `example.invalid` addresses. No Gmail, Google, Azure, real document data or application secrets are needed.

### Isolation is enforced, not assumed

- An unexpected collaborator call fails a strict Moq mock. `Verify`, `Times.Once`, `Times.Never`, `VerifyNoOtherCalls` and `MockSequence` check observable interactions.
- HTTP responses are returned from an in-process mocked message handler. No socket is opened. Disposal is explicitly allowed without weakening `SendAsync` expectations.
- Session signing uses an EF context with **no configured database provider**. It does not pretend that an EF in-memory provider reproduces PostgreSQL semantics. Any database query in that selected path would fail.
- Real CSV serialization and JWT validation execute. Mocking the subject itself would conceal the behavior the tests need to check.
- Cancellation and dependency failures are injected directly, without sleeps or random network faults. Clock-sensitive token assertions allow the interval captured immediately before and after creation; random JWT IDs are checked for valid identity rather than compared to a fixed token string.

## Setup and commands

Required: .NET SDK 10.x, PowerShell (Windows PowerShell or PowerShell 7), Python 3 standard library. Docker is optional and provides the reliable fallback when local assembly instrumentation is restricted. Test tooling versions: xUnit 2.5.3, Moq 4.20.72, Microsoft.NET.Test.Sdk 17.14.1, Coverlet collector 6.0.4. The last two are test-only package updates.

Run from the repository root. On Windows use `python` or an absolute Python path; on Ubuntu CI use `python3`.

```powershell
# New 90-case contribution, with coverage and machine-readable evidence.
./tools/testing/Run-Unit.ps1 -PythonExecutable python -ResultsDirectory artifacts/unit-run-01

# Existing unit regression plus new cases in both reused projects.
./tools/testing/Run-Unit.ps1 -IncludeExisting -PythonExecutable python -ResultsDirectory artifacts/unit-regression-01

# Evidence-tool safeguards, separate from the .NET case count.
python -B -m unittest discover -s tools/testing -p test_summary.py -v
```

`-IncludeExisting` explicitly excludes `OnboardingUniquenessTests`, a single pre-existing PostgreSQL integration test. This keeps the unit runner independent of services, avoids silently skipped results, and records that exclusion in `run.json`. The existing ordinary backend CI job continues to own the integration case and its isolated database. This command is not a full-system regression claim.

### Offline Docker execution

First restore dependencies normally with .NET, and make the SDK image available while online:

```powershell
dotnet restore tests/FleetFlow.IdentityService.Tests/FleetFlow.IdentityService.Tests.csproj
dotnet restore tests/FleetFlow.VehicleService.Tests/FleetFlow.VehicleService.Tests.csproj
docker pull mcr.microsoft.com/dotnet/sdk:10.0
dotnet nuget locals global-packages --list
```

Use the cache path printed by the last command. This laptop's configured cache is shown below; another laptop should supply its own path.

```powershell
./tools/testing/Run-Unit.ps1 -Engine Docker `
  -NuGetCache 'Z:\Programs\FleetFlowCaches\nuget' `
  -PythonExecutable python -ResultsDirectory artifacts/unit-offline-01

./tools/testing/Run-Unit.ps1 -Engine Docker -IncludeExisting `
  -NuGetCache 'Z:\Programs\FleetFlowCaches\nuget' `
  -PythonExecutable python -ResultsDirectory artifacts/unit-regression-offline-01
```

The disposable SDK container uses `--network none`, no published port, read-only source and read-only NuGet cache. It copies source into a container workspace excluding `bin`/`obj`, and writes only to the selected host results directory. It cannot reach PostgreSQL, Kafka or Azure. The existing application and its containers are not stopped or altered.

Package vulnerability fetching is disabled **only for that offline container command**, because network isolation makes the NuGet advisory service unavailable. Native execution and ordinary CI retain their normal audit behavior. This is not a vulnerability-scan result. A missing package/image causes failure and should be fixed during preparation, not by removing isolation during the demo.

On this laptop, a first native focused run passed, but a subsequent coverage-instrumented run encountered Windows Application Control error `0x800711C7`. Use the verified Docker route rather than disabling the security policy. Do not present the blocked run as passing.

## Evidence and acceptance rules

Each run produces:

| File | Purpose |
| --- | --- |
| `run.json` | UTC execution time, source commit, dirty-tree flag, engine, filter, exclusions and process exit status |
| Per-project `.trx` | Actual discovered cases, individual outcomes, durations and failure details |
| `coverage.json`, `coverage.cobertura.xml` | Raw Coverlet measurements, suitable for inspection or downstream tooling |
| `summary.json`, `index.html` | Aggregated outcomes and coverage by production type; HTML opens without a server |
| `SHA256SUMS.txt` | Integrity manifest for the captured files |
| Execution log(s) | Compiler, test-host and execution output; Docker also records SDK/runtime information |

The summarizer fails if either expected project's results or coverage is absent, TRX is duplicated, tests are empty/failed/skipped, or process execution failed. Conflicting coverage files in one project are rejected; identical VSTest attachment copies are deduplicated. Covered source locations are combined as a union, not an average of project percentages. Nine self-tests guard these acceptance rules. Malformed report input also stops execution rather than producing a passing summary.

Coverlet includes the **five full production types** listed in `tools/testing/unit.runsettings`, not the entire application. `AuthenticationService` includes database-backed methods outside the selected signing path, so their uncovered branches remain in the denominator. The report must not describe these percentages as whole-system coverage or remove uncovered production methods to inflate them. Coverage describes execution; assertions and the separate mutation contribution address fault detection. No mutation score is claimed here.

## Scope boundaries and follow-on work

### Assessment traceability

| Individual criterion | Reviewable evidence |
| --- | --- |
| Code quality and standards | Existing projects, typed theories, strict Moq expectations, reusable harnesses, unchanged production code |
| Implementation depth | Invalid identities, dependency outages/cancellation, JWT cryptography, Unicode/formula safety, row and integer-overflow boundaries |
| Live demo reliability | Offline Docker execution, fresh result directories, preserved logs and a rehearsable fallback |
| Walkthrough and clarity | Two-minute script, per-case TRX, per-type coverage and explicit scope explanations |
| VIVA and concept mastery | Ten implementation-specific questions in `unit-demo.md`; understand and rehearse the code before presenting |

The CI job and ownership README support the group automation criterion. Completing the other three assigned layers and rehearsing the joint twelve-minute handover remain team tasks.

Direct controller invocation verifies action logic. It does **not** run ASP.NET routing, authorization middleware, model binding, JWT middleware or a live HTTP pipeline. Controller-level ownership checks and delegated portal errors are covered; full role enforcement remains the existing integration/E2E layer's responsibility. Likewise, these tests do not prove database uniqueness, distributed booking correctness, SMTP delivery or deployed performance.

For Jadavan's Stryker work, useful first targets are:

1. Staff list bound `> 1000`, identity comparisons, duplicate detection and timeout/cancellation filters.
2. CSV limit `> 10000`, widened count addition, formula-prefix protection and quote escaping.
3. Missing-subject guards, forced-password renewal conditions and exception/status mappings.
4. UTF-8 key-length comparison and session claim values.

Start with the existing test-project references and `Category=ASE.Unit` suites. Evaluate surviving mutants by behavior and equivalence; do not delete assertions or weaken production code to raise a score. Mutation testing is intentionally left to its assigned owner.

## Tool references

- [Moq setup, verification, strict behavior and protected members](https://github.com/devlooped/moq/wiki/Quickstart)
- [Coverlet VSTest collector configuration](https://github.com/coverlet-coverage/coverlet/blob/master/Documentation/VSTestIntegration.md)
- [.NET unit-test coverage workflow](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-code-coverage)

The implementation can be reviewed through source, raw evidence and reproducible commands. No production service, database migration, frontend design or Azure deployment was changed for these tests.
