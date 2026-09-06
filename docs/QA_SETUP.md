# FleetFlow Sprint 1 QA Setup

This guide provides step-by-step instructions to clone, configure, seed, and independently test the FleetFlow platform during Sprint 1 QA verification.

---

## Prerequisites

Ensure the following tools are installed on your local environment:
- **Git** (v2.30+)
- **Docker Desktop** (with Docker Compose support)
- **.NET SDK** (.NET 10.0)
- **Node.js** (v20+ with npm)

---

## Clone Repository

Clone the project repository and check out the latest `develop` branch:

```bash
git clone https://github.com/IT24103433/Fleet_Flow.git
cd Fleet_Flow
git checkout develop
git pull --ff-only origin develop
```

---

## Start PostgreSQL

Launch the local PostgreSQL container using Docker Compose:

```bash
docker compose up -d postgres
```

---

## Seed QA Accounts

Use the dedicated local QA seeding utility to provision the standard four test accounts in PostgreSQL.

### PowerShell
```powershell
$env:QA_SEED_PASSWORD="your-local-test-password"
dotnet run --project tools/SeedQaUsers
```

### Bash / macOS / Linux
```bash
export QA_SEED_PASSWORD="your-local-test-password"
dotnet run --project tools/SeedQaUsers
```

> [!IMPORTANT]
> - `QA_SEED_PASSWORD` is an environment variable used for local verification only.
> - Plaintext passwords and secrets are **never** committed to Git or hardcoded in source files.
> - This utility is strictly for development and local QA testing; it is not used for production data.

---

## Start Backend Services

### 1. Start IdentityService
Open a new terminal and run:

```bash
dotnet run --project src/backend/services/IdentityService/IdentityService.Api/IdentityService.Api.csproj
```
* **Expected URL:** `http://localhost:5001`

### 2. Start FleetService
Open a second terminal and run:

```bash
dotnet run --project src/backend/services/FleetService/FleetService.Api/FleetService.Api.csproj
```
* **Expected URL:** `http://localhost:5002`

---

## Start Frontend

Open a third terminal and run:

```bash
cd src/frontend
npm install
npm run dev
```

* **Expected Default URL:** `http://localhost:5173`  
*(If Vite selects another port such as `5174` due to port occupation, use the actual port printed in your terminal).*

---

## Health Checks

Verify that both backend services and database connectivity are healthy:

```bash
curl.exe -i http://localhost:5001/health
curl.exe -i http://localhost:5002/health
```

**Expected Response:**
```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8

{"status":"Healthy","database":"Connected"}
```

---

## QA Test Accounts

The QA seeding tool provisions four accounts corresponding to the platform's RBAC matrix:

| Username | Email | Assigned Role | Target Portal |
| :--- | :--- | :--- | :--- |
| `admin` | `admin@fleetflow.io` | `ADMIN` | Staff / Admin Portal |
| `manager` | `manager@fleetflow.io` | `FLEET_MANAGER` | Staff Portal (Fleet Operations) |
| `worker` | `worker@fleetflow.io` | `MAINTENANCE_STAFF` | Staff Portal (Maintenance Queue) |
| `customer` | `customer@fleetflow.io` | `CUSTOMER` | Customer Portal |

*(The password for all four accounts is the value supplied to `$env:QA_SEED_PASSWORD` during the seeding step).*

---

## Application Portals

* **Customer Portal (Login):** [http://localhost:5173/#/login](http://localhost:5173/#/login)
* **Staff & Management Portal (Login):** [http://localhost:5173/#/staff-login](http://localhost:5173/#/staff-login)
* **Public Landing Page:** [http://localhost:5173/#/landing](http://localhost:5173/#/landing)
* **Browse Fleet Catalog:** [http://localhost:5173/#/browse](http://localhost:5173/#/browse)

---

## Sprint 1 QA Verification Areas

### 1. Customer Experience
- **Login / Logout:** Public login with valid JWT token issuance.
- **Customer Home:** Active session landing view with quick actions.
- **Browse Fleet Catalog:** Search and filter demonstration models.
- **Vehicle Details UI:** Spec sheet inspection, interactive gallery thumbnails, and reservation prompt.
- **Customer Profile UI:** Personal details view, contact edit modal, photo dialog, and change password flow.

### 2. Fleet Manager Experience
- **Staff Login:** Dark-theme operations entry gate.
- **Fleet Manager Dashboard:** Role-aware operational metrics.
- **Fleet Management UI:** Operational inventory table, vehicle inspection, and status modal.
- **Vehicle Image Management UI:** Photo gallery showcase and reordering preview.

### 3. Maintenance Staff Experience
- **Staff Login:** Role gate recognition for `MAINTENANCE_STAFF`.
- **Maintenance Dashboard:** Maintenance work queue and vehicle service alerts.

### 4. Administrator Experience
- **Admin Dashboard:** Four-role RBAC distribution matrix and audit shortcuts.
- **User Management UI:** User directory table, role filtering, and administrative password reset modal.
- **User Provisioning UI:** 4-role user creation form with temporary password generation.
- **Security & Inspection UI:** JWT token claim inspector.

### 5. RBAC Cross-Service Enforcement
Backend RBAC authorization matrix verified via `FleetService` (`GET /weatherforecast`):
- **Anonymous / No Token:** `401 Unauthorized`
- **`CUSTOMER` Token:** `403 Forbidden`
- **`MAINTENANCE_STAFF` Token:** `403 Forbidden`
- **`FLEET_MANAGER` Token:** `200 OK`
- **`ADMIN` Token:** `200 OK`

---

## Known Intentionally Incomplete Functionality (Sprint 2 Roadmap)

The following areas are intentionally planned for Sprint 2 backend integration:
- Real Vehicle REST backend APIs (`POST /api/vehicles`, `GET /api/vehicles`, `PUT /api/vehicles`)
- Vehicle CRUD database persistence
- Cloud/Object vehicle image storage and upload persistence
- Customer/Staff profile database persistence
- Admin Create User API endpoint
- Admin Reset Password API endpoint
- Server-enforced Force Password Change gate
- Granular permission backend policies
- Email-based password recovery and SMTP integration
- Booking and reservation backend microservice
- Maintenance task scheduling backend microservice
