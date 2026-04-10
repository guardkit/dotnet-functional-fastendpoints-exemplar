---
id: TASK-W4
title: "API Host — Program.cs, Keycloak auth, DbUp, appsettings"
status: completed
task_type: feature
wave: 4
created: 2026-04-10T00:00:00Z
updated: 2026-04-10T00:00:00Z
completed: 2026-04-10T00:00:00Z
priority: high
complexity: 5
parent_review: TASK-59B3
feature_id: dotnet-fastendpoints-exemplar
dependencies: [TASK-W1, TASK-W2, TASK-W3]
tags: [dotnet, fastendpoints, auth, keycloak, program]
previous_state: in_review
completed_location: tasks/completed/TASK-W4/
quality_gates:
  build: passed
  map_inbound_claims_false: passed
  customer_lookup_no_double_instantiation: passed
  dbup_idempotent: passed
  core_unit_tests: "14/14 passed"
organized_files:
  - TASK-W4.md
---

# TASK-W4: API Host

## Scope

Wire everything together in `Exemplar.API`: composition root (`Program.cs`), Keycloak JWT auth, DbUp migration runner on startup, health checks, and configuration files.

## Deliverables

| # | Task | Output |
|---|------|--------|
| 29 | `Program.cs` — registration order: OTEL → Serilog → Auth → DB → Repositories → Services → FastEndpoints → HealthChecks → DbUp | Composition root |
| 30 | `AuthenticationExtensions.cs` — `MapInboundClaims = false`, role mapping, 3 policies | Keycloak auth |
| 31 | `KeycloakHealthCheck.cs` — HTTP GET to Keycloak OIDC discovery endpoint | Health check |
| 32 | `appsettings.json` + `appsettings.Development.json` | All required config keys, documented |

## Quality Gates

- [x] `dotnet build` — zero warnings, zero errors
- [ ] Application starts and `/health/live` returns 200 — requires Docker/Keycloak/PG (TASK-W5)
- [x] `MapInboundClaims = false` present and commented
- [x] `ICustomerLookup` is resolved from the same `CustomerService` instance as `ICustomerService` (no double-instantiation)
- [x] DbUp runs migrations on startup and is idempotent (safe to run twice)

## Implementation Notes

**Key decisions made during implementation:**

- `JwtBearer` pinned to `8.*` — wildcard `*` resolved to .NET 10 build which is incompatible with `net8.0` target
- `KeycloakHealthCheck` uses `IHttpClientFactory` (not direct `HttpClient`) — correct DI pattern for health checks avoids lifetime issues
- FastEndpoints assembly scanning is explicit: `typeof(CreateCustomer).Assembly` + `typeof(AddAddress).Assembly` — deterministic, no accidental endpoint discovery
- Both health checks tagged `"ready"` — liveness probe runs zero checks, readiness gates on Keycloak + PostgreSQL
- `AddCustomers()` must be called before `AddAddresses()` — `AddressService` depends on `ICustomerLookup` being registered first
- `UseAuthentication()` + `UseAuthorization()` added to pipeline in `Program.cs` — `UseApiConfiguration()` only sets up FastEndpoints/Swagger

## Next Wave

TASK-W5 (Database + Docker) adds migrations SQL, docker-compose, Keycloak realm config.
