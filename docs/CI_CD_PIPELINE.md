# FleetFlow CI/CD Pipeline Architecture & Operations Guide

This guide documents the Continuous Integration and Continuous Deployment (CI/CD) pipelines in FleetFlow, detailing stage separation, artifact flow, pipeline monitoring, and troubleshooting runbooks.

---

## 1. Pipeline Architecture & Job Separation

The FleetFlow CI/CD pipeline is designed with **strict job isolation**. Every distinct phase of the verification lifecycle executes in a separate job. This ensures that failures are immediately traceable to a specific stage (compilation, automated testing, static analysis, or cloud deployment) without digging through monolithic job logs.

```mermaid
flowchart TD
    subgraph Trigger ["1. Triggers"]
        P["Git Push / PR (main, develop, feat/*)"]
    end

    subgraph CI_Pipeline ["2. Continuous Integration Pipeline (ci.yml)"]
        direction TB
        subgraph Backend_Track ["Backend Track"]
            B1["backend-build<br/>(.NET 10 compile & package)"] --> B2["backend-test<br/>(xUnit test runner & TRX)"]
        end

        subgraph Frontend_Track ["Frontend Track"]
            F1["frontend-lint<br/>(ESLint code quality)"]
            F2["frontend-test<br/>(Node.js test suite)"]
            F1 & F2 --> F3["frontend-build<br/>(Vite production bundle)"]
        end

        B2 & F3 --> GATE["ci-quality-gate<br/>(All quality standards validated)"]
    end

    subgraph CD_Pipeline ["3. Continuous Deployment (deploy-*.yml)"]
        direction TB
        D_BUILD["CD Build Stage<br/>(Compile release target)"] --> D_DEPLOY["CD Deploy Stage<br/>(Azure App Service Publish)"]
    end

    GATE -.->|Triggers on main merge| CD_Pipeline
```

---

## 2. CI/CD Stage Breakdown & Failure Attribution

| Job / Stage | Scope & Tools | Outputs / Artifacts | How Failure is Attributed |
| :--- | :--- | :--- | :--- |
| **`backend-build`** | `dotnet restore`<br>`dotnet build --configuration Release`<br>`dotnet publish` | `backend-binaries`<br>(IdentityService, FleetService, RentalService, NotificationService & Tools) | **Compilation / Syntax / Missing NuGet Package**.<br>If this fails, no backend tests run. |
| **`backend-test`** | `dotnet test --no-build`<br>Runs 630+ xUnit & integration tests | `backend-test-results`<br>(TRX logs) | **Backend Logic Regression / Assertion Failure**.<br>TRX artifact contains exact failing test case and stack trace. |
| **`frontend-lint`** | `npm run lint`<br>ESLint 9.x | Console report | **Frontend Formatting / Code Style / Linter Error**.<br>Flags unused variables, invalid hooks, or syntax issues. |
| **`frontend-test`** | `npm test`<br>160+ client unit tests | Console report / TAP | **Client-side State / Logic / Validation Error**.<br>Flags client regression without waiting for bundle compilation. |
| **`frontend-build`** | `npm run build`<br>Vite production bundler | `frontend-dist`<br>(Production static bundle) | **Vite Bundle Error / Asset Resolution Failure**.<br>Only runs after linting and tests pass. |
| **`ci-quality-gate`** | Aggregation barrier | Pipeline badge green status | **Overall Quality Assurance Gate**.<br>Guarantees all sub-systems are verified before deployment. |
| **`deploy-*`** | Isolated `build`, `deploy`, and `smoke-probe` jobs | Azure App Service deployment | **Environment / Credential / Cloud Warmup Failure**.<br>Includes automated `/health/ready` smoke probe with retries. |
| **`deploy-orchestrator`** | Master phased coordinator (`Identity` $\rightarrow$ `Fleet` + `Rental` + `Notification` $\rightarrow$ `Frontend`) | End-to-end synchronized release | **Release Order / Inter-Service Dependency Failure**.<br>Prevents race conditions by verifying upstream service readiness first. |

---

## 3. Deployment Security & Cloud Readiness

### 1. Passwordless Authentication via GitHub OIDC
The CD workflows and orchestrator support GitHub OpenID Connect (OIDC) via `azure/login@v2` with automated fallback to publish profiles:
- Set `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, and `AZURE_SUBSCRIPTION_ID` in GitHub Actions Secrets.
- Eliminates static XML credentials and grants scoped RBAC permissions.

### 2. Automated Post-Deployment Smoke Probing
Deployment stages now include automated readiness polling:
- Pings `https://<service-name>.azurewebsites.net/health/ready` up to 15 times (75s window).
- Guarantees cold-start database connection pool warmup before completing the job.
- Fails the pipeline immediately if an unhandled startup crash occurs.

### 3. Cloud Kafka & Azure Event Hubs (SASL/TLS)
All microservices (`IdentityService`, `FleetService`, `RentalService`, `NotificationService`) support cloud-managed Kafka endpoints:
- Set `Kafka__SecurityProtocol=SaslSsl` and `Kafka__SaslMechanism=Plain`.
- Provide `Kafka__SaslUsername=$ConnectionString` and `Kafka__SaslPassword=<EventHubsConnectionString>` to connect directly to Azure Event Hubs.

## 3. Best Practices & Pipeline Health Governance

### 1. Test Locally Before Presentation and Merge
Never rely solely on remote CI to catch errors on presentation day.
- Run `.\build.ps1` (Windows) or `./build.sh` (Linux/macOS) prior to pushing.
- Both scripts execute the exact sequence run by the GitHub Actions runners.

### 2. Prompt Triage: Fix Broken Builds Immediately
Broken builds must not be left unaddressed:
1. **Identify the Failing Stage**: Look at the GitHub Actions workflow summary matrix. The red icon immediately identifies whether it is `backend-build`, `backend-test`, `frontend-lint`, `frontend-test`, or `frontend-build`.
2. **Examine the Artifacts / Logs**:
   - For `backend-test` failures, download the `backend-test-results` artifact to inspect the `.trx` file.
   - For frontend errors, check the step console output.
3. **Reproduce Locally**:
   ```powershell
   # Run only backend tests
   dotnet test FleetFlow.sln

   # Run only frontend tests
   cd src/frontend ; npm test -- --run
   ```
4. **Deploy Immediate Fix**: Branch from `develop` using `bugfix/<issue-name>`, apply the fix, verify locally, and submit a PR.

### 3. Pipeline Monitoring
- **Workflow Badges**: Embedded in `README.md` to display real-time build status.
- **GitHub Notifications**: Developers receive email and browser notifications for any failed run on their branches.
- **Action Run History**: Accessible via `https://github.com/IT24103433/Fleet_Flow/actions`.
