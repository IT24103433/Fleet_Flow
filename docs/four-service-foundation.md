# Four-service foundation: Sprint 4 architecture PR

This PR adds independently buildable Rental and Notification API hosts, owned database initialization, internal authentication, and packaging. **Bookings and notifications still run in Fleet.** The frontend still uses Identity and Fleet; no cutover, booking migration, inbox migration, rental-lifecycle feature, or Azure change occurs here. The two new hosts report `businessActivated: false`. Four operational business services require the subsequent extraction PRs.

## Service boundaries and local interfaces

| Service | Port | Owned database | Foundation state |
|---|---:|---|---|
| IdentityService.Api | 5001 | fleetflow_auth | Existing accounts, roles and authentication; adds scoped token broker |
| FleetService.Api | 5002 | fleetflow_fleet | Existing vehicles, maintenance, bookings and notifications remain active |
| RentalService.Api | 5003 | fleetflow_rental | Host and schema marker only; booking extraction follows |
| NotificationService.Api | 5004 | fleetflow_notifications | Host and schema marker only; inbox/outbox extraction follows |

New services contain no shared EF persistence models and cannot configure connections to the legacy databases. Existing IDs and event identifiers are references in `FleetFlow.Contracts`; they do not create cross-database foreign keys. Maintenance remains in Fleet's future vehicle-availability consistency boundary. The reservation and event DTOs are version-one foundation contracts; business endpoints arrive with their extraction PRs.

Each API exposes `/health/live`, `/health/ready` and `/openapi/v1.json` with Swagger at `/swagger`. Liveness has no database dependency. Rental/Notification readiness requires their owned version-one schema marker and matching owner, otherwise it returns 503 without connection details. Their existing `/health` alias has the same readiness semantics. Identity and Fleet retain their existing `/health` contracts and gain `/health/ready` aliases/checks.

The new `/api/service` endpoints require browser/user JWT authentication. `/internal/service` in Fleet, Rental and Notification explicitly selects the internal JWT scheme and its required scope. No placeholder booking or inbox endpoint returns fabricated data.

## Internal authentication

Identity provides `POST /internal/service-token` using client credentials, not a browser access token. Issuance is disabled by default. When enabled:

- The separate internal signing key and each supplied client secret must contain at least 32 UTF-8 bytes. The signing key must differ from the user JWT key. Keep all values outside Git and browser environment variables.
- Tokens expire after 300 seconds with audience `FleetFlow.Internal`, issuer `FleetFlow.IdentityService`, `token_kind=service`, unique `jti`, and fixed scopes. HS256, issuer, audience, signature and expiry are validated with zero clock skew. A browser token cannot authorize an internal route; an internal token cannot authorize a browser route.
- Rental receives `fleet.read`, `fleet.reservations.write`, `identity.access.read`; Fleet receives `rental.reports.read`, `identity.access.read`, `identity.staff.read`; Notification receives `identity.access.read`, `notifications.operations`. Callers cannot request arbitrary grants.
- Broker requests are limited to 4 KB, 60 requests per minute per remote address, and marked `Cache-Control: no-store`. Unknown credentials receive a generic 401; disabled issuance returns 503. Non-Development requests require HTTPS.
- The service-token client caches tokens until 30 seconds before expiry, serializes concurrent refreshes, times out after five seconds and refuses redirects. Authentication outages fail the operation instead of falling back to a browser token or stale data.
- Identity URLs reject user-info, paths, queries and fragments. HTTP is restricted to loopback or the explicit local `identity-service` Compose hostname in Development; production requires HTTPS. Configure trusted HTTPS forwarding at the deployment ingress before production enablement.

Settings: `SERVICE_AUTH_ENABLED`, `SERVICE_AUTH_SIGNING_KEY`, `SERVICE_AUTH_RENTAL_SECRET`, `SERVICE_AUTH_FLEET_SECRET`, `SERVICE_AUTH_NOTIFICATION_SECRET`, and `INTERNAL_IDENTITY_URL`. Environment disable overrides a section-level enable. Identity needs the three client credentials; other deployed services should receive only their own credential. The Compose overlay follows that split. Internal HS256 verification currently shares a signing key among trusted backend services; this does not provide asymmetric signer isolation. Production key rotation, HTTPS ingress and per-service secret provisioning remain DevOps requirements.

## Local startup (PowerShell)

Keep the current application running from its current checkout during review. Do not start another Identity/Fleet on occupied ports. Startup helpers do not stop existing processes, initialize schemas, or launch hidden replacement services.

1. Copy `.env.example` to `.env` in the chosen checkout if absent; preserve any existing `.env`. Supply the actual local database password and private user signing key. The helper maps `JWT_SECRET`, issuer/audience and Kafka aliases to the existing .NET settings. It reads simple assignments without evaluating shell code and imports values only into the current process. It rejects remote database hosts and an inherited connection-string override.
2. Use the existing infrastructure when PostgreSQL/Kafka are already running. For a new environment, `docker compose up -d` starts only infrastructure. Fresh-volume `init-db.sql` creates all four database names; it never drops or alters an existing database. The historical `admin` superuser block remains a local development compatibility setting, not production provisioning. Do not reset an existing Docker volume to obtain new databases.
3. Inspect each new database with a read-only dry run:

```powershell
./tools/Initialize-ServiceDatabase.ps1 -Service rental
./tools/Initialize-ServiceDatabase.ps1 -Service notifications
```

4. On the verified loopback instance, initialize only the new foundation databases:

```powershell
./tools/Initialize-ServiceDatabase.ps1 -Service rental -Apply
./tools/Initialize-ServiceDatabase.ps1 -Service notifications -Apply
```

5. Run one service per terminal:

```powershell
./tools/Start-Services.ps1 -Service identity
./tools/Start-Services.ps1 -Service fleet
./tools/Start-Services.ps1 -Service rental
./tools/Start-Services.ps1 -Service notifications
```

The API overlay is opt-in:

```powershell
docker compose -f docker-compose.yml -f docker-compose.services.yml --profile apps config --quiet
docker compose -f docker-compose.yml -f docker-compose.services.yml --profile apps up -d --build
```

This second command is a deliberate local startup action, not part of the default infrastructure command. Ensure host API ports are free first. Uploaded account and vehicle photos use persistent volumes. The overlay is Development-only and leaves Kafka disabled unless explicitly configured; it does not migrate inbox ownership or start a second notification writer. The overlay adds an internal Kafka listener (`kafka:19092`) while retaining the host listener on port 9092. Applying that overlay to an existing infrastructure stack deliberately recreates the broker configuration; it was not applied to the running stack during this PR.

## Guarded database initializer

`tools/ServiceDatabaseTool` is published separately. Default execution is a dry run. `--apply` is required for writes:

```powershell
dotnet run --project tools/ServiceDatabaseTool -- --service rental
dotnet run --project tools/ServiceDatabaseTool -- --service rental --apply
```

It accepts only a loopback admin connection to database `postgres`. The selected destination is exactly `fleetflow_rental` or `fleetflow_notifications`, or a validated service-specific `_foundation_test_` database. It creates a missing destination and records the normalized SQL SHA-256, version and service owner transactionally. An advisory lock serializes schema initialization. Repeated application validates the stored checksum and performs no writes. Unknown existing tables, wrong ownership or changed checksum cause refusal. SQL is packaged with the tool. Connection failures never print credentials. The tool cannot drop a database, alter legacy tables, transfer business records, or cut over a writer.

Hosts can receive `RENTAL_DB_USER/PASSWORD` and `NOTIFICATION_DB_USER/PASSWORD` separately. DevOps must provision least-privilege database roles, owned grants and HTTPS connection settings for deployed environments; shared local PostgreSQL credentials are not an authorization boundary.

## CI and verification

The root `FleetFlow.sln` now includes all four APIs, shared libraries, initializer and all five backend test projects. The backend solution also includes the two new APIs and foundation tests. CI publishes four distinct API directories and the database tool. The ordinary CI workflow still runs on feature branches/PRs. The legacy develop deployment remains manual-only.

CI runs the existing PostgreSQL concurrency checks and new initialization tests against an ephemeral `fleetflow_hardening_ci` database, with an explicitly CI-only password. Foundation tests create unique owned `_foundation_test_` databases, verify dry-run, repeated application, wrong-owner/checksum refusal and data preservation, then remove only those disposable databases. No test imports working application connection strings. Automatic TRX filenames retain each project's report instead of overwriting one shared filename. Node tests run with the current test-runner contract (`npm test`).

Evidence: [test and artifact manifest](sprint4-foundation-verification.json). Published Rental/Notification hosts were smoke-tested on isolated loopback ports 15003/15004 (liveness 200, unavailable-database readiness 503, anonymous metadata 401, OpenAPI 200) and stopped afterward. Published SQL hashes match source. Compose configuration, PowerShell syntax and Docker build-definition checks passed; no images were built or deployed.

Local result: **502 backend tests passed, zero failures/skips** (466 preserved plus 36 foundation cases). New coverage includes fixed grants, invalid credentials, token expiry/audience/algorithm, browser/internal isolation, role scope denial, liveness/readiness, broker HTTPS/cache rules, token refresh/outages and real PostgreSQL ownership/migration guards. Three nullable warnings remain in unchanged legacy Identity test files. The frontend is unchanged from the accepted 126-test baseline; CI reruns its tests, lint and build. No new entry-JavaScript or homepage chunks are introduced by this PR.

## Recovery and extraction gate

For this foundation PR, revert its commit and stop only the two new API processes to return to the previous topology. Keep the new databases and uploaded files rather than deleting data. Their schema markers do not affect the legacy services or frontend. Preserve the already verified frontend archives; no frontend rollback is needed here.

Before either extraction PR makes business-data changes: back up `fleetflow_auth`, `fleetflow_fleet` and uploads; hash and restore them into isolated verification databases; export affected IDs, timestamps, read states and relationships. Migrate with preserved IDs and verify counts/checksums. Introduce the Fleet reservation ledger and persisted idempotent Rental workflows before activating Rental bookings. Cut over under a controlled write pause with exactly one writer and compatibility forwarding. After new-service writes, rollback must reconcile under a write pause; it cannot simply switch back to stale legacy tables. Those migration and rollback tools belong to the extraction PRs, not this empty-database initializer.

Next after human merge: Notification extraction plus S4-07 (outbox, retry, deduplication, failed-delivery visibility and replay). Rental extraction follows in its own PR. The 12-story/36-point Sprint 4 baseline is unchanged; architecture foundation is additional work. S4-12 remains open until DevOps deploys and QA verifies the actual release.

Sources: [service data ownership](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/architect-microservice-container-applications/data-sovereignty-per-microservice), [orchestrated saga recovery](https://learn.microsoft.com/en-us/azure/architecture/patterns/saga).
