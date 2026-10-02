# FleetFlow Performance Engineering & Load Testing Suite

Welcome to the FleetFlow Load Testing and Benchmark Repository maintained by **Aqqil Mohammed (IT24104314)**.
This workspace contains production-grade load testing specifications, automated k6 stress payloads, continuous benchmark metric datasets, and in-depth performance bottleneck analyses across all four FleetFlow microservices.

---

## 1. Executive Charter & Objectives

The primary objective of this performance engineering initiative is to validate that FleetFlow meets high-availability, low-latency, and high-concurrency Service Level Objectives (SLOs) prior to general production release.

### Target Architecture Overview
FleetFlow is architected as an asynchronous, event-driven microservices ecosystem built on **.NET 10 (C#)**, **PostgreSQL 16**, **Apache Kafka 3.7**, **Redis 7.2**, and **Docker/Kubernetes**:

- **IdentityService** (`http://localhost:5001` / `http://identity-service:8080`):
  - Handles customer registration, multi-channel authentication (Customer, Fleet Manager, Admin), JWT issuance, and RBAC claim enforcement.
  - PostgreSQL database: `fleetflow_identity`
- **FleetService** (`http://localhost:5002` / `http://fleet-service:8080`):
  - Manages vehicle catalogs, inventory status lifecycles, maintenance scheduling, and operational analytics.
  - PostgreSQL database: `fleetflow_fleet`
- **RentalService** (`http://localhost:5003` / `http://rental-service:8080`):
  - Orchestrates rental bookings, check-in/pickups, returns, rate calculations, and vehicle reservations.
  - PostgreSQL database: `fleetflow_rental`
- **NotificationService** (`http://localhost:5004` / `http://notification-service:8080`):
  - Consumes transactional outbox events from Kafka, delivering transactional emails, SMS notifications, and system alerts.
  - PostgreSQL database: `fleetflow_notification`

---

## 2. Directory Layout

```
docs/aqqil/
├── README.md                                  # Load testing overview, runbooks, and sprint benchmark log
├── specs/
│   └── LoadTestPlanAndSLOs.md                 # Concurrency targets, ramp-up curves, p95/p99 SLOs, error budgets
├── load-tests/
│   └── api-stress-payloads.json               # Large structured JSON request/response test fixtures
├── benchmarks/
│   ├── identity-fleet-latency.csv             # Latency, throughput, CPU, and DB connection metrics (Identity & Fleet)
│   └── rental-notification-throughput.csv      # Booking concurrency, Kafka lag, and outbox throughput metrics
└── reports/
    └── PerformanceBottleneckAnalysis.md       # Root-cause analysis, flamegraph reviews, and release readiness sign-off
```

---

## 3. Sprint Benchmark Progression Log

| Run ID | Date | Target Service | Concurrency (VUs) | Target Endpoint | Observed p95 (ms) | Target SLO (ms) | Status | Commit / Changes |
|:-------|:-----|:---------------|:------------------|:----------------|:------------------|:----------------|:-------|:-----------------|
| BM-101 | 2026-09-01 | Workspace Init | N/A | N/A | N/A | N/A | INITIALIZED | Initialized benchmark workspace |
| BM-102 | 2026-09-02 | Cross-Service Specs | 10-500 VUs | All Services | N/A | Defined in Specs | BASELINED | Drafted Concurrency Targets & p95 SLOs |
| BM-103 | 2026-09-04 | IdentityService | 50 VUs | /api/auth/register, /login | N/A | < 65 ms | PREPARED | Added 85 structured Auth stress fixtures |
| BM-104 | 2026-09-05 | Identity & Fleet | 10-50 VUs | /auth/login, /vehicles | 28.4 ms | < 65 ms | PASSED | Recorded baseline latency CSV metrics |
| BM-105 | 2026-09-07 | FleetService | 100 VUs | /api/vehicles/search | N/A | < 75 ms | PREPARED | Expanded payloads with Fleet search & filter matrices |
| BM-106 | 2026-09-09 | Identity & Fleet | 50 VUs | /auth/login, /vehicles | 22.8 ms | < 65 ms | OPTIMIZED | Pruned cold-start warm-up rows from latency logs |
| BM-107 | 2026-09-10 | IdentityService | 100 VUs | /api/auth/login, /api/auth/verify | 34.8 ms | < 65 ms | PASSED | Documented RBAC token validation overhead |
| BM-108 | 2026-09-12 | Rental & Notification | 50-100 VUs | /api/bookings, /notifications | 72.5 ms | < 130 ms | PASSED | Added initial rental-notification throughput CSV |
| BM-109 | 2026-09-14 | RentalService | 150 VUs | /api/bookings (Concurrent) | N/A | < 130 ms | PREPARED | Added concurrent booking collision payloads |
| BM-110 | 2026-09-14 | RentalService | 150 VUs | /api/bookings (Schema) | N/A | < 130 ms | REFACTORED | Normalized schema & pruned duplicate Sprint 1 fixtures |
| BM-111 | 2026-09-16 | FleetService | 150 VUs | /api/vehicles, /api/maintenance | 38.4 ms | < 75 ms | PASSED | Documented Postgres connection pool saturation & fix |
| BM-112 | 2026-09-18 | FleetService | 250 VUs | /api/vehicles (Pagination) | 36.8 ms | < 75 ms | PASSED | Recorded 250 VU ramp-up for catalog pagination |
| BM-113 | 2026-09-20 | NotificationService | 200 VUs | Kafka Outbox & Event Streaming | 42.0 ms | < 500 ms lag | PASSED | Added Kafka streaming & outbox metrics |
| BM-114 | 2026-09-21 | FleetService | 250 VUs | /api/vehicles/search | 41.5 ms | < 75 ms | RECALIBRATED | Cleaned up docker bridge timeout spikes |
| BM-115 | 2026-09-23 | FleetService | 50 VUs | /api/maintenance/dashboard | 32.4 ms | < 95 ms | PASSED | Documented maintenance work-order index findings |
| BM-116 | 2026-09-25 | RentalService | 100 VUs | /api/rentals/pickup, /complete | N/A | < 85 ms | PREPARED | Added Sprint 4 pickup & return stress payloads |
| BM-117 | 2026-09-27 | NotificationService | 150 VUs | Outbox Worker Soak | 24.8 ms | < 500 ms lag | PASSED | Recorded 4-hour soak test metrics for outbox workers |
| BM-118 | 2026-09-28 | FleetService | 150 VUs | /api/vehicles (Traces) | 24.6 ms | < 50 ms | OPTIMIZED | Replaced superseded Sprint 2 drafts with updated traces |
| BM-119 | 2026-09-30 | Reports & Analytics | 50 VUs | /api/reports/export/csv | 68.0 ms | < 150 ms | PASSED | Added CSV export memory & throughput stress results |
| BM-120 | 2026-10-02 | IdentityService | 250 VUs | /api/auth/revocation | 38.2 ms | < 65 ms | HARDENED | Logged access revocation hardening impact on SLOs |

---

## 4. Benchmark Harness & Tooling Stack

- **Load Generator:** Grafana k6 (v0.52.0) with distributed execution via k6-operator
- **Metrics Telemetry:** OpenTelemetry .NET SDK with Prometheus scraping endpoint (`/metrics`)
- **Tracing & Profiling:** Jaeger distributed tracing with OpenTelemetry Collector
- **Database Telemetry:** PostgreSQL `pg_stat_statements`, `pg_stat_activity`, and Npgsql internal connection pool counters
- **Message Broker Monitoring:** Kafka Exporter with Prometheus Kafka consumer group lag dashboards

---

## 5. Execution Quickstart

To trigger the standard benchmark suites locally against the test environment:

```bash
# Run baseline smoke test
k6 run --vus 10 --duration 1m load-tests/scripts/smoke-test.js

# Run full ramp-up stress test
k6 run --config load-tests/k6-stress-profile.json load-tests/scripts/full-suite.js
```
