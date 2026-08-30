# Role-Based Access Control (RBAC) Documentation

This document outlines the backend Role-Based Access Control (RBAC) architecture, role definitions, token claims, cross-service validation, and technical verification scaffolding for FleetFlow.

---

## 1. System Roles

The FleetFlow platform defines three standard roles:

1. **`CUSTOMER`**:
   - Default role automatically assigned to standard users during public registration.
   - Intended for end-users interacting with client applications (e.g., viewing public listings, managing personal bookings).
   - Denied access to internal fleet management operations.

2. **`FLEET_MANAGER`**:
   - Dedicated role for fleet and operations staff.
   - Authorized to manage vehicles, maintenance schedules, and operational assignments.
   - Assigned only via administrative mechanisms (never assigned during public self-registration).

3. **`ADMIN`**:
   - System administrator role with global supervisory access.
   - Authorized to access administrative, operational, and auditing endpoints across all services.

### Idempotent Startup Seeding
During startup initialization (`IdentityService`), an idempotent role-seeding routine guarantees that the `CUSTOMER`, `FLEET_MANAGER`, and `ADMIN` roles exist in the database without producing duplicates, altering existing user profiles, or modifying existing `UserRoles` relationships.

---

## 2. JWT Configuration and Shared Validation

`IdentityService` and consuming microservices (e.g., `FleetService`) adhere to identical JWT configuration parameters:

* **Issuer:** `FleetFlow.IdentityService`
* **Audience:** `FleetFlow.Client`
* **Signing Key Configuration:** Loaded dynamically from `Jwt:Key` (with environment variable override `Jwt__Key`). Production keys are never hard-coded in source files or configuration templates.
* **Token Lifetime & Skew:** Default lifetime of 60 minutes with `ClockSkew = TimeSpan.Zero` for immediate expiration enforcement.

### Standard Role Claim Mapping
- Roles are embedded into issued JWT tokens as standard `ClaimTypes.Role` (`http://schemas.microsoft.com/ws/2008/06/identity/claims/role`) claims.
- The ASP.NET Core `Microsoft.AspNetCore.Authentication.JwtBearer` handler maps these claims to `ClaimsPrincipal.IsInRole()` automatically, preserving standard claim mapping behavior without requiring custom claim keys or token mutation.

---

## 3. Authentication vs. Authorization & Status Codes

| Concept | Scope | Status Code on Failure |
| :--- | :--- | :--- |
| **Authentication** (Who you are) | Token presence, cryptographic signature verification, issuer/audience validation, and expiration check. | **`401 Unauthorized`** |
| **Authorization** (What you can do) | Role membership checks via `[Authorize(Roles = "...")]` attributes after a token is authenticated. | **`403 Forbidden`** |

### Request Pipeline Ordering
In both services, authentication middleware is positioned strictly before authorization middleware:
```csharp
app.UseAuthentication();
app.UseAuthorization();
```

---

## 4. Temporary Verification Scaffolding

To verify cross-service RBAC without prematurely coupling domain logic or exposing insecure test endpoints:

1. **`IdentityService` (`GET /weatherforecast`):**
   - Protected with `[Authorize]`.
   - Requires any authenticated user regardless of role.

2. **`FleetService` (`GET /weatherforecast`):**
   - Protected with `[Authorize(Roles = "FLEET_MANAGER,ADMIN")]`.
   - Enforces the following test matrix:
     - **Anonymous / No Token:** `401 Unauthorized`
     - **Invalid / Expired Token:** `401 Unauthorized`
     - **`CUSTOMER` Token:** `403 Forbidden`
     - **`FLEET_MANAGER` Token:** `200 OK`
     - **`ADMIN` Token:** `200 OK`

> [!NOTE]
> `WeatherForecastController` serves exclusively as temporary verification scaffolding for Sprint 1 RBAC validation. Future business endpoints (e.g., `VehicleController`, `BookingController`) will adopt real role restrictions, while advanced resource-level authorization (e.g., resource ownership verification) remains deferred to future sprints.
