# FleetFlow — Enterprise Vehicle Fleet Management Platform

[![FleetFlow CI/CD Pipeline](https://github.com/IT24103433/Fleet_Flow/actions/workflows/ci.yml/badge.svg)](https://github.com/IT24103433/Fleet_Flow/actions/workflows/ci.yml)
[![Build Status](https://img.shields.io/badge/Build-Passing-brightgreen.svg)]()
[![Automated Tests](https://img.shields.io/badge/Tests-272%20Passed-success.svg)]()
[![.NET Version](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![Node Version](https://img.shields.io/badge/Node.js-22.x-green.svg)](https://nodejs.org/)

**FleetFlow** is an enterprise-grade, microservice-oriented vehicle fleet management system featuring role-based access control (RBAC), vehicle lifecycle tracking, maintenance scheduling, and real-time Kafka event streaming.

---

## Table of Contents

- [Repository Structure](#repository-structure)
- [Prerequisites](#prerequisites)
- [Unified Build & Verification Script](#unified-build--verification-script)
- [Local Development Setup](#local-development-setup)
  - [1. Environment Configuration](#1-environment-configuration)
  - [2. Starting Databases & Kafka with Docker](#2-starting-databases--kafka-with-docker)
  - [3. Running Backend Services](#3-running-backend-services)
  - [4. Running Frontend SPA](#4-running-frontend-spa)
  - [5. Seeding QA & Initial Users](#5-seeding-qa--initial-users)
- [Running Automated Tests](#running-automated-tests)
- [Branching Strategy & Governance](#branching-strategy--governance)
- [CI/CD Pipeline & Stage Separation](#cicd-pipeline--stage-separation)
- [Pre-Presentation Verification Checklist](#pre-presentation-verification-checklist)

---

## Repository Structure

The codebase is organized with a clean separation of concerns:

```
Fleet_Flow/
├── .github/
│   └── workflows/                # CI/CD GitHub Actions pipelines
│       ├── ci.yml                # Main CI pipeline with strict job separation
│       ├── deploy-fleet-service.yml
│       ├── deploy-identity-service.yml
│       ├── deploy-frontend.yml
│       └── develop_fleetflow-app-24103433.yml
├── docs/                         # Project architecture & process documentation
│   ├── BRANCHING_STRATEGY.md     # GitFlow guidelines, branch naming, protection
│   ├── CI_CD_PIPELINE.md         # CI/CD architecture, job separation, monitoring
│   ├── QA_SETUP.md               # QA test setup and test credentials
│   ├── notification-management.md
│   └── reports-analytics.md
├── infra/                        # Infrastructure as Code (IaC) & deployment
│   ├── main.bicep                # Azure resource provisioning template
│   └── deploy.ps1                # Azure deployment and secret provisioning script
├── src/                          # Application source code
│   ├── backend/                  # .NET 10 microservices
│   │   ├── services/
│   │   │   ├── IdentityService/  # Authentication, JWT, and RBAC service
│   │   │   └── FleetService/     # Vehicle catalog, status, and maintenance service
│   │   └── FleetFlow.sln         # Backend solution file
│   └── frontend/                 # React 19 + Vite single page application
│       ├── src/                  # Components, pages, hooks, state, and API services
│       ├── package.json          # Frontend dependencies and scripts
│       └── vite.config.js        # Vite build configuration
├── tests/                        # Backend test projects
│   ├── FleetFlow.IdentityService.Tests/ # Identity unit & integration tests (101 tests)
│   └── FleetFlow.VehicleService.Tests/  # Fleet & Vehicle tests (69 tests)
├── tools/
│   └── SeedQaUsers/              # QA and test user database seeder
├── build.ps1                     # Unified build and test script for Windows
├── build.sh                      # Unified build and test script for Linux/macOS
├── docker-compose.yml            # Local PostgreSQL and Kafka services
├── .env.example                  # Environment configuration template
└── FleetFlow.sln                 # Root solution link
```

---

## Prerequisites

Before running the project locally, ensure the following dependencies are installed:

- **[.NET SDK 10.0](https://dotnet.microsoft.com/download)** (v10.0.x or later)
- **[Node.js](https://nodejs.org/)** (v22.x LTS recommended)
- **[Docker & Docker Desktop](https://www.docker.com/)**
- **Git**

Verify your environment by running:
```powershell
dotnet --version
node --version
npm --version
docker --version
```

---

## Unified Build & Verification Script

FleetFlow includes a cross-platform build script that validates prerequisites, restores dependencies, compiles both the backend and frontend, and runs the entire automated test suite.

### Windows (PowerShell)
```powershell
# Run complete build and test suite
.\build.ps1

# Quick build skipping tests
.\build.ps1 -SkipTests

# Clean previous build artifacts and compile
.\build.ps1 -Clean
```

### Linux / macOS (Bash)
```bash
chmod +x ./build.sh

# Run complete build and test suite
./build.sh

# Quick build skipping tests
./build.sh --skip-tests

# Clean and rebuild
./build.sh --clean
```

---

## Local Development Setup

### 1. Environment Configuration

Copy the example environment configuration into `.env`:
```powershell
Copy-Item .env.example .env
```

Review `.env` to customize database credentials, JWT secrets, and port bindings if needed.

### 2. Starting Databases & Kafka with Docker

Spin up PostgreSQL (with pre-configured schema initialization) and Apache Kafka:
```bash
docker compose up -d
```

Verify containers are running:
```bash
docker compose ps
```

- **PostgreSQL**: `localhost:5432` (Databases: `fleetflow_auth`, `fleetflow_fleet`)
- **Apache Kafka**: `localhost:9092`

### 3. Running Backend Services

You can launch the backend services using the `dotnet` CLI or Visual Studio:

#### Terminal 1 — Identity Service (Port 5001):
```powershell
cd src/backend/services/IdentityService/IdentityService.Api
dotnet run
```
- Swagger UI: `http://localhost:5001/swagger`

#### Terminal 2 — Fleet Service (Port 5002):
```powershell
cd src/backend/services/FleetService/FleetService.Api
dotnet run
```
- Swagger UI: `http://localhost:5002/swagger`

### 4. Running Frontend SPA

#### Terminal 3 — Vite Frontend (Port 5173):
```powershell
cd src/frontend
npm install
npm run dev
```
Open your browser at `http://localhost:5173`.

### 5. Seeding QA & Initial Users

To populate the local database with pre-configured users across all roles (`ADMIN`, `FLEET_MANAGER`, `MAINTENANCE_STAFF`, `CUSTOMER`), run the seed utility:
```powershell
dotnet run --project tools/SeedQaUsers/SeedQaUsers.csproj
```
*(Refer to [`docs/QA_SETUP.md`](docs/QA_SETUP.md) for full credential matrix).*

---

## Running Automated Tests

The solution contains **272 automated tests** across both backend microservices and the frontend client.

### Backend Tests (xUnit)
```powershell
dotnet test FleetFlow.sln --configuration Release
```
- `FleetFlow.IdentityService.Tests`: 101 tests (Authentication, RBAC, Admin controls)
- `FleetFlow.VehicleService.Tests`: 69 tests (Inventory, Status transitions, Bookings)

### Frontend Tests (Node.js Test Runner)
```powershell
cd src/frontend
npm test -- --run
```
- 102 unit & validation tests covering role authorization rules, vehicle editing, and maintenance status workflows.

---

## Branching Strategy & Governance

FleetFlow enforces a GitFlow / GitHub Flow branching model to guarantee release quality:

- **`main`**: Production-ready code. **Strictly protected**.
- **`develop`**: Primary integration branch. **Protected**.
- **`feat/*`**: Feature branches for new functionality.
- **`bugfix/*`**: Bug fixes branched from `develop`.
- **`qa/*`**: Test automation and validation suites.
- **`hotfix/*`**: Urgent production patches branched from `main`.

### Branch Protection Best Practices:
1. Direct pushes to `main` and `develop` are disabled.
2. Every pull request requires at least 1 peer approval.
3. All status checks (`backend-build`, `backend-test`, `frontend-lint`, `frontend-test`, `frontend-build`) must be green before merging.
4. Squash merging is enforced to maintain a clean linear history.

*For complete details, see [docs/BRANCHING_STRATEGY.md](docs/BRANCHING_STRATEGY.md).*

---

## CI/CD Pipeline & Stage Separation

The Continuous Integration pipeline (`.github/workflows/ci.yml`) is architected with **strict job isolation** so that any failure is immediately attributable to an exact stage:

```
[ Git Push / PR ]
       │
       ├─► backend-build   ──► backend-test ──┐
       │                                       ├──► ci-quality-gate ──► [ Ready for Deploy ]
       └─► frontend-lint   ──┐                 │
           frontend-test   ──┴─► frontend-build ┘
```

1. **`backend-build`**: Compiles C# projects and packages binaries.
2. **`backend-test`**: Runs xUnit tests and exports TRX test reports.
3. **`frontend-lint`**: Verifies ESLint standards.
4. **`frontend-test`**: Executes client validation and component test suites.
5. **`frontend-build`**: Compiles optimized Vite production bundle.
6. **`ci-quality-gate`**: Ensures all gates passed before allowing PR merge or deployment.

*For complete pipeline architecture and incident triage runbooks, see [docs/CI_CD_PIPELINE.md](docs/CI_CD_PIPELINE.md).*

---

## Pre-Presentation Verification Checklist

To guarantee seamless evaluations and presentations, follow this verification checklist:

- [x] **Repository Structure**: Clean organization (`/src`, `/docs`, `/tests`, `/infra`, `/tools`).
- [x] **Branch Strategy Documented**: Documented in `docs/BRANCHING_STRATEGY.md` with naming rules.
- [x] **Unified Build Script Tested**: `build.ps1` and `build.sh` tested and 100% green before presentation day.
- [x] **CI/CD Job Separation**: Separate build, test, and deploy stages in `.github/workflows/`.
- [x] **272 Automated Tests Passing**: All backend and frontend test suites passing without failures.
- [x] **Local Setup Instructions Validated**: Step-by-step instructions tested for clean machines and evaluators.
- [x] **Main Branch Protected**: Protection rules documented and configured on GitHub.