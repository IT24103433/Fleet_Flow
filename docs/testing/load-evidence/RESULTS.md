# Aqqil - verified load results

Captured on 20 October 2026 against the actual local Identity and Fleet APIs backed by PostgreSQL. Clean tested source: `e2edeb329d20cbcb79f420c38bd52a287e4c8cf3`.

| Profile | Requests | Requests/s | Overall p95 | Peak virtual users |
| --- | ---: | ---: | ---: | ---: |
| Baseline | 123 | 12.10 | 52.25 ms | 1 |
| Ramp | 1,665 | 55.43 | 67.85 ms | 10 |
| Stress | 5,733 | 142.49 | 374.44 ms | 50 |

All functional checks and configured thresholds passed; HTTP failures were zero. General HTTP totals include three setup requests. Read each original `summary.json` and `run.json` under `evidence/load/` for exact metrics, thresholds, provenance and exit status.

The workload performs a real login, vehicle list and vehicle detail request. Smoke, baseline, ramp and stress profiles are implemented. Hosted CI runs the bounded smoke profile; the three rows above are the separate native measurements.

Acceptance: HTTP failures below 1%; 100% functional checks; overall/login p95 below 2,000 ms; vehicle list/detail p95 below 1,000 ms. These are project hypotheses, not official SLAs. Short local runs with up to 50 virtual users do not establish production capacity.

See `repository/docs/testing/load.md` for design, reproduction commands, a two-minute demo and ten VIVA questions with answers. The shared environment disables Kafka and uses legacy onboarding/rental/notification settings; those advanced modes are outside this assessment.
