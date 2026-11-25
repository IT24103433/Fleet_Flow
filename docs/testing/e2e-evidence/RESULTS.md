# Varun - verified browser results

Captured on 20 October 2026 using installed Chrome against the real React UI, Identity/Fleet APIs and PostgreSQL. Clean tested source: `e2edeb329d20cbcb79f420c38bd52a287e4c8cf3`.

**Five tests passed; zero failures, skips or retries. Recorded test duration: approximately 15.5 seconds.**

1. Empty login validates required fields and focuses the username.
2. Invalid credentials show the real HTTP 401 error.
3. A customer logs in through the UI and reaches the booking workspace.
4. A customer is denied the staff portal with a real HTTP 403.
5. Vehicle catalogue filtering, empty state, reset and vehicle details work with seeded data.

Extract the whole ZIP, then open `evidence/browser/html/index.html`; keep the adjacent `data` folder for its five screenshots. JSON, JUnit, execution log and source/run metadata are also included. With Playwright installed, `npx playwright show-report <path-to-html-folder>` opens the report viewer.

Acceptance: all five real journeys pass, no skipped/focused/flaky tests, no automatic retries, and reports are present. Failed runs retain screenshots, traces and video; this passing local run has success screenshots and does not contain invented failure artifacts. An earlier genuine failed selector run is available at https://github.com/IT24103433/Fleet_Flow/actions/runs/39402727642; the selectors were corrected before the passing captures.

No route mocks or injected login sessions are used. Coverage is limited to these authentication and catalogue journeys; it does not establish complete booking, email/OTP, Kafka, staff-operation or cross-browser coverage.

See `repository/docs/testing/e2e.md` for setup, commands, a two-minute demo and ten VIVA questions with answers. Windows with installed Chrome can use `ASE_BROWSER_CHANNEL=chrome`; install Playwright's FFmpeg dependency as documented so failure-video recording can start correctly.
