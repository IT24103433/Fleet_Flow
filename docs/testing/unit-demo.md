# Sanjeevsaran's two-minute demonstration

**Sanjeevsaran J. | IT24104155 | Group 27 | Unit Testing and Mocking**

## Prepare before the slot

1. Open `MaintenanceStaffDirectoryTests.cs` at the strict handler setup and `DependencyHttpFailure_IsNotTreatedAsEligibleStaff` theory.
2. Open `ReportCsvSafetyTests.cs` at `Maintenance_RowLimit_IncludesExactBoundary` and the overflow test.
3. Open the committed evidence HTML and its `run.json`. Know the recorded source commit and coverage scope.
4. Restore the two projects, cache the .NET SDK Docker image, confirm Docker is running, and rehearse the exact command on the demonstration laptop. Do not depend on venue Wi-Fi.
5. Choose a new output directory, such as `artifacts/viva-unit-01`. Keep the previously captured evidence open as a transparent fallback; do not call old output a new live run.

## Timed walkthrough

| Time | Show and say |
| --- | --- |
| 0:00-0:15 | "My area is unit testing and mocking. These tests execute Identity and Fleet code without starting any API, database or broker. The new contribution contains 90 cases." |
| 0:15-0:35 | Show the strict HTTP-handler and token-provider mocks. Explain that the real directory service creates the request, while the handler supplies failures in memory. Point out exact Bearer/path verification and `Times.Never` after token failure. |
| 0:35-0:50 | Show the 9,999/10,000/10,001 row theory and the counted-list overflow double. Explain why an exact upper boundary catches a `>` to `>=` defect and why fake counts avoid allocating billions of rows. |
| 0:50-1:25 | Start the rehearsed runner command below. While it runs, explain that `--network none` proves the selected tests do not depend on running services. Point to the individual case names and zero failed/skipped totals. |
| 1:25-1:50 | Open the newly created HTML summary. Explain the two projects, source revision, raw TRX/Cobertura links and coverage denominator. Coverage of five types is not application coverage or a mutation score. |
| 1:50-2:00 | "The existing CI quality gate also depends on this job. Jadavan can now mutate these boundaries to assess whether the assertions detect changed behavior." Hand over to the next member. |

The fresh offline container includes compilation, so its duration depends on the laptop. Rehearse it first; if startup exceeds the allocated interval, use the already-built native command below on a laptop where instrumentation is permitted. Avoid installing or restoring packages during the live slot.

```powershell
# Full, reproducible evidence command. Substitute the actual Python/cache paths.
./tools/testing/Run-Unit.ps1 -Engine Docker `
  -NuGetCache 'Z:\Programs\FleetFlowCaches\nuget' `
  -PythonExecutable python -ResultsDirectory artifacts/viva-unit-01

# Short live subset after a successful local build; no coverage collection here.
dotnet test tests/FleetFlow.VehicleService.Tests/FleetFlow.VehicleService.Tests.csproj `
  -c Release --no-build --no-restore --filter FullyQualifiedName~AseUnit.MaintenanceStaffDirectoryTests
```

The short command demonstrates 22 directory cases only. Do not describe it as a fresh run of all 90 cases. Use the full saved report to discuss the wider contribution, and say when that evidence was captured.

## Ten VIVA questions and concise answers

**1. What is a unit in these tests?**
A production function or class tested through observable behavior with its external collaborators replaced. For example, `MaintenanceStaffDirectory` and the real `HttpClient` run, but the message handler returns in-memory responses. No service boundary is crossed.

**2. What is the difference between a stub and a mock here?**
A stub supplies data or an exception. A mock additionally verifies interactions. The reporting double supplies a snapshot, then we verify the exact query/token and one call. A strict mock also rejects unexpected calls immediately.

**3. Why use theories instead of separate facts for every value?**
The behavior is the same while its inputs vary. Inline data makes boundary values visible; typed member data handles exception objects and invalid account lists. xUnit discovers each row separately, so failed input values appear in TRX output.

**4. Are you testing Moq rather than FleetFlow?**
No. We never mock the subject. Real production code maps failures, builds authenticated requests, serializes CSV and signs tokens. Moq controls only dependencies. Assertions verify meaningful output and whether forbidden work occurred.

**5. Why mock `HttpMessageHandler` instead of `HttpClient`?**
`HttpClient` sends through its handler. Replacing protected `SendAsync` preserves real request construction and authorization headers while preventing socket use. It is deterministic and can return malformed JSON or fail without a server.

**6. Why is cancellation different from a dependency outage?**
A cancelled caller no longer needs a response; its cancellation must propagate. An uncancelled caller experiencing a dependency timeout gets an unavailable-domain error. Conflating them breaks cancellation semantics and can misreport infrastructure health.

**7. Do these controller tests prove authorization middleware works?**
No. They test action-level ownership guards and delegated error mapping. Direct method calls bypass routing and authorization filters. Middleware and cross-service access need the existing integration tests and Varun's E2E workflows.

**8. Why not use a database in the signing test?**
Session creation receives an already-loaded account. A providerless context ensures this selected path does not query storage. Database constraints and transactions require PostgreSQL integration tests; an EF in-memory substitute cannot establish those properties.

**9. How do coverage and mutation testing differ?**
Coverage records executed lines/branches; it cannot tell whether assertions would detect wrong output. Mutation changes production behavior and asks whether tests fail. Our boundary and exact-output assertions provide useful targets for Jadavan, but this contribution does not invent a mutation score.

**10. How can you trust the report?**
The runner preserves process failure, writes commit/engine/filter metadata, and retains raw TRX and Coverlet files. The summarizer rejects empty, failed, skipped, duplicate or incomplete results. Its nine self-tests exercise those rejection rules. Hash manifests detect file changes, though they are integrity checks rather than signed attestations.

## Explain these details confidently

- A valid signed JWT is actually validated against issuer, audience, lifetime and signing key. Claim values are checked, not only token non-emptiness.
- The signing boundary is UTF-8 **bytes**: sixteen `é` characters occupy 32 bytes. A character-count check would be wrong.
- Formula-like user strings and real negative decimal amounts have different CSV treatment. Test both so protection does not corrupt legitimate numbers.
- Test count is not an achievement on its own. The useful evidence is which risks and failure branches the cases detect.
- Existing integration tests remain outside the offline unit filter. Neither local unit coverage nor the committed CI configuration proves a deployed system has passed all layers.
