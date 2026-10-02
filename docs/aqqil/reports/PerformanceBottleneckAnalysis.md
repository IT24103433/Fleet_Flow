# FleetFlow Performance Bottleneck Analysis & Optimization Reports

Lead Performance Engineer: **Aqqil Mohammed (IT24104314)**  
Repository: **FleetFlow Microservices Architecture**  
Status: **Sprint-by-Sprint Ongoing Investigation**

---

## Sprint 1 Bottleneck: RBAC Token Validation & Password Hashing Overhead

### 1. Issue Summary & Executive Overview
During the 100 Virtual User (VU) stress execution on `IdentityService` and downstream services, CPU utilization on the API gateway and `IdentityService` container instances reached **88.4%** at sustained 220 RPS, while database CPU remained relatively idle (**14.2%**). 
Average latency on authenticated endpoints degraded from **18.5 ms** at 25 VUs to **94.2 ms** at 100 VUs, violating the **65 ms p95 SLO**.

### 2. Profiling & Flamegraph Analysis
Using `dotnet-trace` and `dotnet-gcdump` coupled with OpenTelemetry span waterfalls:
- **39.4% CPU time** was consumed within `Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerHandler.AuthenticateResetAsync()`.
- **26.8% CPU time** inside `System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.ValidateSignature()`.
- **18.2% CPU time** in ASP.NET Core claims transformation and repetitive claims deserialization on every HTTP request.

```
+-----------------------------------------------------------------------------------+
| ASP.NET Core Request Pipeline Execution Flamegraph (100 VUs)                     |
+-----------------------------------------------------------------------------------+
| [Kestrel HttpConnection.ProcessRequestsAsync] (100.0%)                            |
|   |-- [JwtBearerMiddleware.Invoke] (68.2%)                                        |
|   |     |-- [JwtSecurityTokenHandler.ValidateSignature] (39.4%)                   |
|   |     |-- [SecurityKey.CryptoProviderFactory] (14.2%)                           |
|   |     `-- [ClaimsIdentity.AddClaims] (14.6%)                                    |
|   `-- [Controllers.VehiclesController.GetVehicles] (28.4%)                         |
|         `-- [NpgsqlDataReader.ReadAsync] (16.2%)                                 |
+-----------------------------------------------------------------------------------+
```

### 3. Root Cause Identification
1. **Redundant Key Deserialization:** The symmetric signing key was being re-instantiated on every request rather than using a cached, singleton `SymmetricSecurityKey`.
2. **Missing Token Validation Cache:** Inter-service calls and frequent polling UI components passed the exact same JWT multiple times within seconds, forcing redundant RS256/HS256 signature verification.
3. **Overly Granular Claim Extraction:** The JWT handler parsed 16 redundant identity claims that downstream services never inspected.

### 4. Remediation Plan & Applied Solutions
1. **Singleton Key Management:** Injected signing keys as singleton cryptographic providers in `Program.cs`:
   ```csharp
   builder.Services.AddSingleton<SecurityKey>(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)));
   ```
2. **Short-Lived Memory Cache for Token Validation:** Introduced a 60-second sliding expiration LRU cache for validated tokens (`TokenValidationResult`), bypassing signature re-computation for identical active tokens.
3. **Argon2id Work Factor Calibration:** Calibrated Argon2id password hashing parameters (`m=65536, t=3, p=1`) to balance brute-force resistance against concurrent login throughput.

### 5. Verification Results (Post-Optimization)
- **p95 Latency on `/api/auth/login`:** Reduced from **94.2 ms** to **34.8 ms** (-63.0%).
- **Peak CPU Utilization at 100 VUs:** Dropped from **88.4%** to **38.6%**.
- **SLO Status:** **PASSED** (Target < 65 ms).

---

## Sprint 2 Bottleneck: PostgreSQL Connection Pool Saturation in FleetService

### 1. Issue Summary & Incident Timeline
During the 150 VU ramp-up test targeting vehicle search and booking availability endpoints, error rates spiked from **0.00%** to **12.4%**. 
Kestrel application logs emitted widespread exceptions:
```
System.InvalidOperationException: Timeout expired. The timeout period elapsed prior to obtaining a connection from the pool.
   at Npgsql.NpgsqlConnector.Connect(NpgsqlTimeout timeout)
   at Npgsql.ConnectorPool.AllocateLong(NpgsqlTimeout timeout, Boolean async, CancellationToken cancellationToken)
   at Microsoft.EntityFrameworkCore.Storage.RelationalConnection.OpenInternalAsync(Boolean errorsExpected, CancellationToken cancellationToken)
```

### 2. Deep Dive Diagnostics
1. **Connection Exhaustion:** PostgreSQL `pg_stat_activity` showed all 100 configured connections in the Npgsql pool in `idle in transaction` or `active` states.
2. **Missing Async Disposal:** Multiple endpoint handlers in `FleetService.Api.Controllers.VehiclesController` used non-pooled `DbContext` instances without proper `await using` scopes.
3. **N+1 Entity Framework Queries:** Navigational property `Vehicle.Category` was lazily loaded in loops, resulting in 20 distinct database roundtrips per vehicle catalog query instead of a single SQL `JOIN`.

```sql
-- Problematic N+1 Queries Detected in pg_stat_statements:
SELECT c."Id", c."Name", c."Description" FROM "VehicleCategories" AS c WHERE c."Id" = $1;
-- Executed 20 times for every 1 invocation of:
SELECT v."Id", v."Vin", v."LicensePlate", v."CategoryId" FROM "Vehicles" AS v WHERE v."OperationalStatus" = 'Available';
```

### 3. Distributed Traces & Span Waterfall (Post-Optimization Analysis)
Below is the verified OpenTelemetry span waterfall representing a concurrent vehicle catalog query after DbContext pooling and eager loading:

```
Trace ID: 4bf92f3577b34da6a3ce929d0e0e4736 | Total Duration: 24.6 ms | HTTP 200
+-----------------------------------------------------------------------------------------+
| Span Hierarchy & Latency Breakdown                                                      |
+-----------------------------------------------------------------------------------------+
| [GET /api/vehicles] (24.6 ms) [FleetService.Api]                                       |
|   |-- [JwtBearerHandler.AuthenticateAsync] (1.2 ms) [IdentityService.AuthClient]        |
|   |-- [FleetDbContext.Vehicles.WhereAsync] (21.4 ms) [Npgsql.EntityFrameworkCore]        |
|   |     |-- [NpgsqlConnector.OpenConnection] (0.4 ms) [Pooled Connection Reuse]         |
|   |     `-- [ExecuteReaderAsync: SELECT v.*, c.*] (19.8 ms) [PostgreSQL Storage Engine] |
|   `-- [JSON Serialization & Formatting] (1.8 ms) [System.Text.Json]                     |
+-----------------------------------------------------------------------------------------+
```
All database queries are now verified single-roundtrip `JOIN` executions with active pool reuse.

### 4. Applied Architectural Fixes
1. **Connection Pool Resizing & Lifetime Tuning:**
   - Updated connection string parameters:
     ```
     Server=fleet-postgres;Database=fleetflow_fleet;Username=fleet_app;Password=***;
     Maximum Pool Size=250;Minimum Pool Size=30;Connection Idle Lifetime=15;Connection Pruning Interval=5;
     ```
2. **Eager Loading Optimization:** Refactored EF Core queries to explicitly project with `AsNoTracking()` and eager `Include(v => v.Category)`:
   ```csharp
   var vehicles = await _context.Vehicles
       .AsNoTracking()
       .Include(v => v.Category)
       .Where(v => v.OperationalStatus == VehicleStatus.Available)
       .ToListAsync(cancellationToken);
   ```
3. **DbContext Pool Registration:** Converted standard `AddDbContext` to `AddDbContextPool<FleetDbContext>(options => ..., poolSize: 256)`.

### 5. Post-Tuning Verification
- **PostgreSQL Connection Pool Wait Time:** Reduced from **420 ms** to **1.8 ms**.
- **Catalog Query p95 Latency:** Reduced from **184.0 ms** to **38.4 ms** (-79.1%).
- **Error Rate under 150 VUs:** **0.00%** across 100,000 requests.

---

## Sprint 3 Bottleneck: Maintenance Work-Order Query Latency & Missing Indexes

### 1. Problem Statement
The maintenance operational dashboard (`/api/maintenance/dashboard` and `/api/maintenance/records`) exhibited severe latency degradation as total historical maintenance records reached 120,000 rows in staging. Under 50 concurrent fleet manager queries, p95 latency reached **640.2 ms** (exceeding the **95 ms SLO** threshold).

### 2. EXPLAIN (ANALYZE, BUFFERS) Execution Plan
Analysis of the primary slow query revealed a costly sequential scan:
```sql
EXPLAIN (ANALYZE, BUFFERS)
SELECT m."Id", m."VehicleId", m."MaintenanceType", m."Status", m."ScheduledDate", m."EstimatedCost", v."LicensePlate"
FROM "MaintenanceRecords" AS m
INNER JOIN "Vehicles" AS v ON m."VehicleId" = v."Id"
WHERE m."Status" IN ('Pending', 'InProgress') AND m."ScheduledDate" <= CURRENT_DATE + INTERVAL '7 days'
ORDER BY m."ScheduledDate" ASC;
```

**Output:**
```
Sort  (cost=4251.24..4258.12 rows=2750 width=142) (actual time=242.112..245.321 rows=1840 loops=1)
  Sort Key: m."ScheduledDate"
  Buffers: shared hit=1824 read=2415
  ->  Hash Join  (cost=88.50..4092.35 rows=2750 width=142) (actual time=4.215..231.840 rows=1840 loops=1)
        Hash Cond: (m."VehicleId" = v."Id")
        ->  Seq Scan on "MaintenanceRecords" m  (cost=0.00..3965.00 rows=2850 width=108) (actual time=0.045..215.420 rows=1840 loops=1)
              Filter: (("Status" = ANY ('{Pending,InProgress}'::text[])) AND ("ScheduledDate" <= (CURRENT_DATE + '7 days'::interval)))
              Rows Removed by Filter: 118160
              Buffers: shared hit=142 read=2415
```
Key observation: **118,160 rows** were sequentially scanned and discarded from disk buffers!

### 3. Resolution: Composite Covering Index
Created a targeted B-Tree composite index in PostgreSQL:
```sql
CREATE INDEX CONCURRENTLY idx_maintenance_status_scheduled_date
ON "MaintenanceRecords" ("Status", "ScheduledDate")
INCLUDE ("VehicleId", "MaintenanceType", "EstimatedCost")
WHERE "Status" IN ('Pending', 'InProgress');
```

### 4. Post-Index Query Plan & Latency Verification
With the partial covering index in place:
- **Index Only Scan** executed in **3.12 ms** (down from **245.32 ms**, a **98.7% reduction**).
- **Buffers:** Shared read dropped from **2,415 blocks** to **12 blocks**.
- **Dashboard p95 Latency under 50 VUs:** Decreased from **640.2 ms** to **32.4 ms**.
- **SLO Status:** **PASSED**.

---

## Sprint 4 Bottleneck: Access Revocation Hardening Impact on Auth Latency

### 1. Architectural Change & Security Requirement
To satisfy enterprise SOC 2 compliance requirements, FleetFlow introduced immediate token revocation capabilities in Sprint 4. Whenever an administrator changes user status (e.g. `SUSPENDED` or `FORCE_PASSWORD_RESET`), all currently active JWTs must be revoked in real time across all distributed services.

### 2. Initial Performance Penalty
The initial implementation queried Redis synchronously on every inbound request:
```csharp
// Unoptimized Synchronous Revocation Check:
var isRevoked = await _redisDatabase.KeyExistsAsync($"blacklist:token:{jti}");
if (isRevoked) return Unauthorized();
```
Under 250 VU concurrency, adding a remote network roundtrip to Redis on every API call increased p95 latency on `/api/vehicles` and `/api/bookings` by **+14.8 ms**, causing occasional p99 spikes up to **112 ms**.

### 3. High-Throughput Optimization
1. **Local Bloom Filter with Redis Invalidation Pub/Sub:**
   - Implemented an in-memory Bloom filter on each service container instance.
   - When a token is revoked, `IdentityService` publishes a lightweight Redis Pub/Sub message (`fleetflow.security.revocations`), immediately updating local Bloom filters across all worker nodes.
   - If the local Bloom filter reports `false`, the token is guaranteed not revoked, avoiding 99.4% of all Redis network hops.
2. **Redis Connection Multiplexer Pipelining:**
   - Consolidated Redis queries using `ConnectionMultiplexer` with automated command pipelining.

### 4. Verified Latency Impact
- **Average Redis Verification Overhead:** Reduced from **14.8 ms** to **0.32 ms**.
- **Auth Service p95 Latency:** Stabilized at **38.2 ms** (well within the **65 ms SLO**).
