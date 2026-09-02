# FleetFlow Load Test Plan and Service Level Objectives (SLOs)

Document Version: **1.0.0-DRAFT**  
Author: **Aqqil Mohammed (IT24104314)**  
Effective Date: **2026-09-02**  
Target Environment: **Staging & Pre-Production Performance Clusters**

---

## 1. Concurrency Tiers & Load Profiles

To accurately simulate production usage from everyday off-peak traffic to high-demand holiday rental spikes, the FleetFlow performance testing framework establishes four discrete concurrency tiers:

### Tier 1: Smoke & Baseline Verification (10 - 25 VUs)
- **Objective:** Validate basic connectivity, routing, and database schema sanity without inducing resource saturation.
- **Duration:** 3 - 5 minutes
- **Traffic Pattern:** Constant arrival rate with 2 requests per second (RPS) per virtual user.

### Tier 2: Typical Working Day Steady-State (50 - 100 VUs)
- **Objective:** Model regular operational hours across all four microservices with concurrent customer browsing, reservation bookings, and fleet manager vehicle audits.
- **Duration:** 15 minutes
- **Traffic Pattern:** Linear ramp-up over 3 minutes, 10 minutes steady state, 2 minutes ramp-down.

### Tier 3: High-Demand Campaign Stress (250 VUs)
- **Objective:** Push microservices near resource boundaries to identify memory leaks, lock contention, and thread pool exhaustion.
- **Duration:** 25 minutes
- **Traffic Pattern:** Step ramp-up (50 -> 100 -> 175 -> 250 VUs) with 5-minute plateaus.

### Tier 4: Peak Holiday Surge & Spike Verification (500 VUs)
- **Objective:** Verify auto-scaling responsiveness, circuit breaker trips, connection pool headroom, and graceful degradation during abrupt flash crowds.
- **Duration:** 10 minutes
- **Traffic Pattern:** Fast ramp (0 to 500 VUs in 60 seconds), 5 minutes hold, immediate step-down.

---

## 2. Service Level Objectives (SLOs) Matrix

All microservices must conform to the following percentile latency targets under steady-state load (100 VUs) and peak stress load (250 VUs):

| Microservice | Endpoint / Operation | HTTP Method | Target p50 | Target p95 (SLO) | Target p99 | Max Error Rate |
|:---|:---|:---:|:---:|:---:|:---:|:---:|
| **IdentityService** | `/api/auth/login` | POST | < 25 ms | < 65 ms | < 120 ms | 0.05% |
| **IdentityService** | `/api/auth/register` | POST | < 45 ms | < 110 ms | < 180 ms | 0.10% |
| **IdentityService** | `/api/auth/refresh-token` | POST | < 15 ms | < 40 ms | < 80 ms | 0.02% |
| **FleetService** | `/api/vehicles` (Catalog List) | GET | < 20 ms | < 50 ms | < 95 ms | 0.05% |
| **FleetService** | `/api/vehicles/search` (Multi-Filter) | GET | < 30 ms | < 75 ms | < 130 ms | 0.05% |
| **FleetService** | `/api/maintenance/dashboard` | GET | < 40 ms | < 95 ms | < 160 ms | 0.10% |
| **RentalService** | `/api/bookings` (Create Booking) | POST | < 50 ms | < 130 ms | < 240 ms | 0.10% |
| **RentalService** | `/api/bookings/{id}/cancel` | POST | < 30 ms | < 80 ms | < 150 ms | 0.05% |
| **RentalService** | `/api/rentals/bookings/{id}/pickup` | POST | < 35 ms | < 85 ms | < 160 ms | 0.05% |
| **NotificationService** | Transactional Outbox Lag | ASYNC | < 150 ms | < 500 ms | < 1200 ms | 0.01% |
| **NotificationService** | WebSocket Notification Push | WS | < 15 ms | < 40 ms | < 90 ms | 0.05% |

---

## 3. Error Budget & Failure Thresholds

1. **Error Budget Allocation:**
   - Over any 10-minute continuous benchmark window, HTTP 5xx responses must represent less than **0.05%** of all completed requests.
   - HTTP 429 (Too Many Requests) is allowed only when synthetic load exceeds Tier 4 stress thresholds (> 500 VUs).

2. **Database Health Bounds:**
   - PostgreSQL maximum connection pool utilization must remain below **80%** of configured `MaxPoolSize`.
   - Lock wait durations exceeding 200 ms trigger critical alert notifications.

3. **Kafka Event Streaming Health:**
   - Event delivery latency from producer commit in `RentalService` to consumer acknowledge in `NotificationService` must not exceed **800 ms** at p95.
   - Outbox processing worker queue lag must recover to zero within 120 seconds following a load burst.

---

## 4. Test Topology & Environmental Constraints

- **App Servers:** 4x Container Nodes (2 vCPU, 4GB RAM allocated per service instance)
- **Database Server:** PostgreSQL 16.2 on Dedicated SSD Instance (4 vCPU, 16GB RAM, max_connections = 300)
- **Kafka Cluster:** 3 Broker Nodes, replication factor = 2, min.insync.replicas = 2
- **Redis Cache:** Redis 7.2 Standalone (2GB RAM, `allkeys-lru` eviction policy)
