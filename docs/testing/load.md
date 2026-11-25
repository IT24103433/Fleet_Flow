# Aqqil - Load and Performance Testing

**Assigned owner: Aqqil M.R.M. | IT24104314 | SE3112 Group 27**

This is the k6 layer prepared with Codex at Jadavan's request. Assigned ownership labels are not a claim of independent authorship or a substitute for Aqqil reviewing, running and explaining the implementation.

## Scope and setup

`tests/load/fleetflow.js` sends real HTTP traffic to the production Identity and Fleet applications backed by a disposable PostgreSQL database. Every iteration logs in through `POST /api/auth/login`, reads `GET /api/vehicles?paged=true&pageSize=20`, and retrieves `GET /api/vehicles/{id}`. Assertions check a real session and username, seeded data/pagination, and the requested vehicle identity. There are no fake API responses.

The shared [assessment environment](system-test-environment.md) uses synthetic accounts and two seeded vehicles in uniquely named test databases. The load workload is read-only apart from ordinary login behavior; it does not create bookings or corrupt application data. Run only against this controlled local setup. The script rejects non-loopback target URLs, and stress needs explicit opt-in. No public deployment receives test load.

Required: k6 **2.3.0**, .NET 10, Node 22, PowerShell and the real assessment environment. Provide `ASE_TEST_PASSWORD` through the environment. Optional `ASE_TEST_USERNAME` defaults to the seeded customer. `IDENTITY_BASE_URL` and `FLEET_BASE_URL` default to local ports 15001 and 15002 and must remain explicit loopback HTTP URLs.

```powershell
./tools/testing/Run-Load.ps1 -Profile smoke -ResultsDirectory artifacts/load-smoke
./tools/testing/Run-Load.ps1 -Profile baseline -ResultsDirectory artifacts/load-baseline
./tools/testing/Run-Load.ps1 -Profile ramp -ResultsDirectory artifacts/load-ramp
./tools/testing/Run-Load.ps1 -Profile stress -AllowStress -ResultsDirectory artifacts/load-stress
```

If k6 is not on PATH, pass `-K6Executable` with its actual executable path. Every output path must be fresh. Real k6 failures and threshold failures remain non-zero; a stress failure is a result to analyze, not a reason to weaken the checks.

## Load design and acceptance criteria

| Profile | Controlled load | Purpose |
| --- | --- | --- |
| smoke | 1 virtual user for 5 seconds | Short bounded CI gate |
| baseline | 1 virtual user for 10 seconds | Low-concurrency reference |
| ramp | 0 -> 5 -> 10 -> 0 VUs, 10 seconds per stage | Observe change as concurrency grows and recovers |
| stress | 0 -> 10 -> 25 -> 50 -> 0 VUs, 10 seconds per stage | Bounded local pressure experiment; not a proven capacity limit |

There is a 0.2-second think time per iteration and bounded request/graceful-stop timeouts. These are closed-model virtual-user workloads; achieved request rate depends on response times. They do not promise a constant arrival rate. Setup requests are included in k6's general HTTP metrics but not in the three per-endpoint custom latency trends.

Project acceptance hypotheses, selected before measuring: less than 1% failed HTTP requests, all functional checks passing, overall/login p95 below 2000 ms, and vehicle list/detail p95 below 1000 ms. These broad local regression limits are not official SLAs or assessment-mandated numbers. Review them against measured hardware and user requirements rather than claiming universal capacity.

`summary.json` retains the actual k6 metrics and threshold outcomes, with assigned owner, profile, targets and capture time. `run.json` records source commit/dirty state and real exit status. The log and tool-version file remain alongside it. Status counters distinguish 2xx, 4xx, 5xx and other/transport outcomes. Interpret p95, throughput and failures together: a faster run with errors is not an improvement.

## CI and demonstration

`ase-load` starts a fresh PostgreSQL-backed application environment, runs the genuine five-second smoke workload, uploads results/service logs even after failure, and gates the final workflow. Ramp and stress runs are excluded from routine PR CI.

Two-minute demo: 20 seconds on endpoint/profile selection; 30 seconds on checks and thresholds; 40 seconds running the bounded smoke and reading actual metrics; 30 seconds comparing recorded baseline/ramp/stress outcomes and explaining one limitation. Do not imply a recorded full stress run is happening live.

## Ten VIVA questions

1. **Why k6?** It supplies scriptable HTTP workloads, virtual-user profiles, checks, thresholds and machine-readable measurements.
2. **Checks versus thresholds?** Checks record response correctness; thresholds determine whether aggregate metrics make the run fail. This suite gates both.
3. **What is p95?** Ninety-five percent of the measured samples are at or below that latency; it describes the tail better than an average alone.
4. **What is throughput?** Completed HTTP requests per second, affected here by concurrency, response times and think time.
5. **Why baseline, ramp and stress?** They establish a reference, observe increasing demand/recovery, and apply a bounded higher workload.
6. **Why test login?** It exercises real password verification and database access, a different cost from read-only catalogue operations.
7. **Can you claim maximum capacity?** No. A 50-VU local experiment is limited by host resources, duration and workload mix. Saturation may not be reached.
8. **How is data protected?** Loopback-only targets, distinct disposable database names, synthetic accounts/vehicles and no booking mutations.
9. **What happens on a threshold failure?** k6 returns non-zero, the runner preserves it, CI fails, and reports remain available for analysis.
10. **What limits the evidence?** One local environment, short closed-model runs, synthetic data, disabled Kafka and legacy onboarding/rental modes. These are not deployed-system performance guarantees.

Reference: [k6 thresholds](https://grafana.com/docs/k6/latest/using-k6/thresholds/).
