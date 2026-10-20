# Jadavan's two-minute mutation demo and VIVA

Use the final source and real evidence. Rehearse this yourself; be able to explain every assertion rather than reading a generated answer verbatim.

## Prepare before the presentation

- Open `mutation.md`, the Stryker config and `AseMutation/MutationRegressionTests.cs`.
- Open the before and after HTML reports under `docs/testing/evidence/mutation/2026-10-20/`.
- Warm the package/tool restore. Keep fresh output paths ready. No service startup is needed.
- Use a non-UTC time zone for the Local-date distinction. The verified machine was Sri Lanka Standard Time; CI sets Asia/Colombo.
- Full Stryker executions took approximately 90 seconds each after preparation, but startup/network/hardware can increase this. Show a real short test run during the two-minute slot and clearly identify the pre-executed full mutation reports.

## Exactly two minutes

| Time | Show / say |
| --- | --- |
| 0:00-0:20 | "My contribution tests the strength of the tests. I reused Sanjeevsaran's xUnit/Moq suite and mutated staff-directory validation and CSV exports. Production code is unchanged." Show the scope and pinned manifest. |
| 0:20-0:50 | Show staff-directory line 23 and `ErrorStatus_WithOtherwiseValidStaffJson_IsStillRejected`. Explain why an empty error body hid a removed HTTP guard. Valid JSON separates the status rule from parsing failure. |
| 0:50-1:15 | Run the eight new regression cases using the command below. Point to the real passing output. |
| 1:15-1:45 | Switch to the genuine before/after reports: 67.68% -> 95.96%; 67 -> 95 killed; valid denominator stays 99. Show the guard mutant changing from survived to killed. |
| 1:45-2:00 | State two message-text survivors and two null-string-fallback coverage gaps remain. Briefly show `ase-unit -> ase-mutation -> ci-quality-gate` and that failure artifacts are retained. |

```powershell
dotnet test tests/FleetFlow.VehicleService.Tests/FleetFlow.VehicleService.Tests.csproj --no-restore --filter Category=ASE.Mutation
```

If the live environment fails, show the authentic saved TRX and report and say explicitly that they came from the recorded 20 October run. Do not present the failed live command as passing.

## Fifteen likely VIVA questions

1. **What is mutation testing?** It introduces small code changes into an instrumented test build and checks whether assertions detect the resulting behavior. Production files on disk are not intentionally rewritten.
2. **How is it different from coverage?** Coverage tells us which code executed. Mutation testing asks whether a changed behavior was detected. An executed line can still have weak assertions.
3. **What does killed mean?** A selected test failed for the mutated version after the original tests passed. This suggests the suite detects that generated fault.
4. **What is a survivor?** A tested mutant did not fail the selected tests. It may expose a missing scenario, weak assertions, behavior outside the intended contract or an equivalent mutation. It needs investigation.
5. **What is your score formula?** Detected mutants are Killed plus Timeout; divide by Killed plus Timeout plus Survived plus NoCoverage, then multiply by 100. CompileError and Ignored are excluded.
6. **Why not claim 100%?** Two exception-message mutations survive and two unusual null-return formatting paths are uncovered. These are visible limitations, not hidden exclusions.
7. **What was your strongest finding?** An error-status guard could be removed while old tests still passed because empty JSON parsing failed later. Valid-looking JSON with a failing HTTP status exposes the difference.
8. **What did you change?** Added eight independent discovered regression cases, the pinned tool/config, reproducible runner, evidence validator, actual reports, analysis and CI coordination. Business logic and Sanjeevsaran's test files are unchanged.
9. **How was the comparison fair?** Same production files, Stryker version, mutation level, scope and thresholds. Only the selected test suite expanded. A comparison script checks mutant locations/operators/replacements and matching base configuration/tool manifest.
10. **What does NoCoverage mean?** None of the selected tests reached the mutant. It remains in the score denominator. Two maintenance-row gaps became killed after adding a mixed-source export.
11. **What does CompileError mean?** The generated change did not produce a valid executable program. It is reported but excluded from the score; it is not an application compile failure claim.
12. **Why does time zone matter?** Relabelling Local as UTC and converting Local to UTC give the same clock value on a UTC host. A non-zero offset distinguishes the behaviors; CI explicitly provides one.
13. **How are CI failures handled?** Mutation needs the passing unit job, checks the original suite again, preserves tool exit status, uploads evidence with `always()`, and blocks the final quality gate if it fails or is skipped.
14. **Why choose 70% as the break threshold?** It is an initial team policy selected before the first completed run, not an assignment rule. It caught the 67.68% baseline; the 95.96% result passed without changing it. The team can review and tighten it later.
15. **What remains before team submission?** Peer review and a real GitHub Actions run, then Aqqil's bounded local load smoke and Varun's browser suite integrated with real service startup, isolated data and failure artifacts. Rehearse all four demos.

## Your slide and pipeline explanation

Slide 3: one small diagram "real unit suite -> Stryker faults -> killed/survived -> stronger assertions", the measured 67.68% -> 95.96% comparison, one HTTP-guard example and explicit remaining gaps. Revisit slide 1 for the shared pipeline; keep the full group deck within the agreed five-slide limit and twelve-minute plan.
