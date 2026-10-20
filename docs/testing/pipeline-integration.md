# Shared test pipeline handover

**Coordinator: Jadavan.** This contribution integrates mutation into the existing workflow, with the completed xUnit/Moq layer as its prerequisite. A full four-technique pipeline cannot be claimed until the load and E2E owners supply runnable implementations and evidence.

## Current implementation

`ase-unit -> ase-mutation -> ci-quality-gate`

Existing backend build/integration and frontend lint/test/build checks also remain prerequisites for the quality gate. The mutation job starts no live services. Its artifact contains logs, source/configuration metadata, the unmutated preflight TRX, actual HTML/JSON mutation reports, summaries and hashes. The gate explicitly rejects failed, cancelled or skipped dependencies instead of printing a success summary for incomplete checks.

Local checks validate workflow syntax and dependency references, and actual native unit/mutation execution. They are not a successful hosted Actions run. After pushing the branch, record the Actions run URL and outcome. If hosted execution exposes a platform difference, fix it and retain both failed and successful evidence.

## Handover required from Aqqil (k6)

- Committed script path and exact short smoke command, plus the k6 version used.
- Actual local API routes, base URLs, ports, authentication setup and startup/health-check commands.
- Isolated synthetic data and repeatable seed/cleanup commands; no external production stress traffic.
- A bounded duration/user count and justified thresholds. Threshold failures must exit non-zero.
- Machine-readable summary/log paths and real baseline/ramp/stress evidence for the assessment.

Add the genuine smoke run as a job in the same workflow once it exists. Provision isolated dependencies, wait for readiness with a bounded timeout, execute k6, and upload logs/results even on failure. Include that job in the final gate. Keep destructive/high-concurrency stress scenarios out of the default PR run.

## Handover required from Varun (Playwright)

- Configuration/test paths, Node/Playwright versions and exact install/run commands.
- Frontend/backend startup commands, base URLs, readiness probes and isolated test accounts.
- Actual journeys and assertions, seed/cleanup behavior and headless CI requirements.
- HTML-report, screenshot and trace paths for failures, with genuine local execution evidence.

Add the real headless browser job to the existing workflow after the implementation is available. Start its own isolated application environment, install required browser dependencies, run the tests and retain reports/traces with `always()`. Include the job in the final gate. A static page-open placeholder is not an E2E contribution.

## Integration acceptance

1. Each owner supplies a reproducible local command and a real outcome.
2. Shared configuration/service-startup changes receive review from affected owners.
3. Every required test layer surfaces a non-zero failure and publishes diagnostics.
4. Final gate lists all implemented required layers and rejects skipped layers.
5. Preserve the final integrated commit, run URL, reports, versions and known limitations.
6. Do not change Azure deployment settings or use production credentials to make assessment tests pass.
