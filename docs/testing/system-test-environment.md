# Controlled local environment for load and browser tests

The shared helper starts the **real** Identity and Fleet applications and the existing Vite frontend. It does not replace production controllers, stub APIs or use an EF in-memory provider. The only required external service is disposable PostgreSQL. Docker is optional on the workstation; GitHub Actions uses a fresh PostgreSQL service container.

## Settings

Provide environment variables to the helper and test runners:

| Variable | Meaning |
| --- | --- |
| `ASE_TEST_PASSWORD` | Synthetic password used only for newly seeded local assessment accounts |
| `ASE_DB_PASSWORD` | Password for the disposable PostgreSQL superuser |
| `ASE_DB_HOST` | `127.0.0.1` or `localhost` only |
| `ASE_DB_PORT` | Local PostgreSQL port; 5432 by default, 15432 for the portable example |
| `ASE_RUN_ID` | New lowercase alphanumeric/underscore suffix, at most 30 characters |
| `ASE_ENV_DIR` | New output folder for environment logs and metadata |
| `ASE_BROWSER_CHANNEL` | Optional `chrome` for an installed local Chrome; omit for Playwright Chromium |

The database names are `fleetflow_ase_<run-id>_identity` and `fleetflow_ase_<run-id>_fleet`. Always use a new run ID. The helper refuses occupied application ports and existing output directories. It does not stop other applications or reuse their data. The PostgreSQL account must be able to create these databases. An existing ordinary development database is not an assessment database.

## Optional Docker-free Windows PostgreSQL

Install the portable dependency into an ignored artifact directory (initial network download required):

```powershell
npm install --prefix artifacts/pg-runtime --save-exact embedded-postgres@18.4.0-beta.17
$env:ASE_DB_PORT='15432'
# Set ASE_DB_PASSWORD in your shell to a synthetic local-only value.
node tools/testing/Start-AssessmentDatabase.mjs artifacts/pg-runtime
```

Leave that terminal running. The helper binds PostgreSQL to loopback only, uses `artifacts/assessment-postgres` by default and creates no system service or OS user. Stop it with Ctrl+C when finished. Its data is retained for inspection. `ASE_DB_DATA_DIR` can choose a fresh data directory; do not reuse an unrelated cluster. The package release suffix is the wrapper's version label; the server's real PostgreSQL version is printed at startup.

## Start the APIs and UI

In another shell with the same assessment variables set:

```powershell
npm ci --prefix src/frontend
node tools/testing/serve-assessment.mjs
```

The helper builds the production API projects serially, starts them at `http://127.0.0.1:15001` and `http://127.0.0.1:15002`, waits for their actual database-readiness endpoints, authenticates the seeded manager through the real API, and creates two controlled vehicles. It starts Vite at `http://127.0.0.1:15173` with explicit local API URLs. It writes `environment.json` only after real readiness and seeding succeed. Service stdout/stderr and build logs are retained separately.

The test environment supplies clearly synthetic JWT/service-client keys, enables actual internal service authentication, disables Kafka and verified-email onboarding, and selects legacy rental/notification ownership. This isolates the assessed login/catalogue journeys from email providers and distributed services without changing business code. Those advanced modes are not covered by this suite.

Stop the helper with Ctrl+C after testing. It tracks and stops its own spawned children; do not manually stop unrelated .NET or browser processes. PostgreSQL is managed separately. Do not commit local credentials or database directories. Only sanitized environment metadata and actual test evidence belong in the handover.

## Reproduction and acceptance

Run [the k6 commands](load.md) or [the Playwright commands](e2e.md) from the repository root with a fresh evidence path. Preserve actual exit status. Inspect `run.json`, raw reports and environment logs together. Database-ready health probes are stronger than a listening port. Setup failures must stop the run; never substitute a passing fixture for an unavailable application.
