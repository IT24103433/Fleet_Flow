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
