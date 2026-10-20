# Varun - UI and End-to-End Testing

**Assigned owner: Varun K. | IT24103696 | SE3112 Group 27**

This is the Playwright layer prepared with Codex at Jadavan's request. Assigned ownership labels are not a claim of independent authorship; Varun should review, reproduce and understand the implementation before presenting it.

## Real application journeys

The isolated `tests/browser` package pins Playwright Test **1.64.1** without changing the frontend's production dependencies. Chromium is the CI target; a locally installed Chrome channel can be selected for Docker-free Windows runs with `ASE_BROWSER_CHANNEL=chrome`.

Five browser scenarios use the actual React UI, actual Identity/Fleet HTTP APIs and a disposable PostgreSQL database. No `page.route` response mocks or injected authentication sessions are used:

1. Empty login validates both required fields and focuses the username field.
2. Incorrect credentials produce the real HTTP 401 and display its error.
3. A customer signs in through the UI and reaches their booking workspace.
4. A customer attempting the staff portal receives a real HTTP 403 and stays on staff login.
5. The catalogue shows real seeded vehicles, a nonmatching filter produces an empty state, clearing it restores results, and opening a vehicle reaches its actual detail page.

The tests use role/label/text locators and Playwright's waiting/assertions rather than arbitrary sleep delays. Each test gets a fresh browser context; account state is not shared through saved storage. The database seed is recreated per environment in isolated names. Tests do not create bookings or depend on production data.

## Run and artifacts

Start the real [assessment environment](system-test-environment.md), then:

```powershell
npm ci --prefix tests/browser
# CI/Linux or when using Playwright's bundled Chromium:
cd tests/browser
npx playwright install --with-deps chromium
cd ../..
./tools/testing/Run-E2E.ps1 -ResultsDirectory artifacts/e2e-run
```

`ASE_TEST_PASSWORD` must match the seed password. For an already installed local Chrome, set `ASE_BROWSER_CHANNEL=chrome` and omit the Chromium download. The base URL is the deliberately fixed local UI at `http://127.0.0.1:15173`; application API URLs are explicitly set by the environment helper. Production deployments are outside this suite.

Each new output folder contains an HTML report, JSON results, JUnit XML, execution log and source/run metadata. Successful journey screenshots are attached to the HTML report. Failed attempts retain screenshots, traces and video. Retries are **zero**: a flaky attempt cannot silently become a passing result. The runner rejects empty, failed, skipped or flaky summaries and preserves non-zero execution status. `forbidOnly` prevents accidentally committing a focused subset.

Open a report with `npx playwright show-report <actual-html-directory>` from `tests/browser`. `npx playwright show-trace <trace.zip>` opens an actual failed trace, when one was produced. Do not invent a failure artifact for a passing run. The suite uses normal login credentials in an isolated database; test artifacts must never be collected with real production accounts.

## Scope limitations and CI

This suite covers authentication/portal boundaries and catalogue journeys, not all FleetFlow functionality. It does not prove OTP/email onboarding, Google sign-in, booking concurrency, all staff operations, rental extraction, Kafka delivery or multiple browser-engine compatibility. The local environment runs legacy onboarding/rental/notification modes with Kafka disabled; these settings are recorded rather than presented as full deployed-system coverage.

`ase-e2e` provisions PostgreSQL, starts the real APIs/UI, installs the locked runner/browser, executes all journeys and uploads evidence even on failure. It gates the final workflow. A configured job is not a passing hosted run until an actual Actions run is recorded.

## Two-minute demo and ten VIVA questions

Demo: 20 seconds on scope/environment; 30 seconds showing a positive and negative assertion; 40 seconds running the prepared browser tests; 30 seconds reading actual HTML evidence and explaining trace retention and coverage limits. Have authentic saved results ready if startup exceeds the live slot.

1. **What makes this E2E?** The browser interacts with real UI controls, the UI calls real production APIs, and those APIs use PostgreSQL; responses are not substituted.
2. **Why not just check that a page opens?** A rendered shell can hide broken authentication or data flows. These tests assert observable outcomes and real HTTP status behavior.
3. **Why role and label locators?** They track how users find controls and are generally less tied to visual styling than CSS-position selectors.
4. **How do you avoid timing flakes?** Playwright waits for locators, responses, URLs and assertions. The suite avoids arbitrary sleeps and uses bounded timeouts.
5. **How is state isolated?** Separate browser contexts, controlled test credentials, read-oriented journeys and disposable per-run database names.
6. **Why a real staff-portal denial?** It proves the server rejects a customer on the wrong channel, alongside the UI message, rather than merely hiding a staff button.
7. **What does a trace show?** Recorded actions, DOM snapshots, network activity and assertion context for diagnosis of a genuine failed attempt.
8. **Why zero retries?** The evidence should expose instability. A failed attempt is not hidden by an automatic successful retry.
9. **What blocks CI?** Any test failure, empty/skipped/flaky result, startup failure or missing report results in a failing job; the final gate includes that job.
10. **What remains untested?** Advanced onboarding, bookings and many staff/distributed workflows, other browser engines, and production deployment behavior.

Reference: [Playwright local web servers and waiting](https://playwright.dev/docs/test-webserver).
