# Implementation Guide — dotnet-fastendpoints-exemplar

**Parent review**: TASK-59B3  
**Report**: `.claude/reviews/TASK-59B3-review-report.md`  
**Goal**: Working .NET 8 solution → input to `/template-create` → GuardKit `dotnet-fastendpoints` built-in template

---

## Execution Strategy

**Mode**: Sequential (waves depend on each other)  
**Testing**: Standard (compile + test pass + ≥80% line coverage per wave)  
**Constraints**: None

Waves must complete in order. Do not start W2 until W1 builds and tests are green.

---

## Wave Overview

| Wave | Task | Scope | Complexity | Dependencies |
|------|------|-------|-----------|--------------|
| 1 | TASK-W1 | Core Foundation | 5 | — |
| 2 | TASK-W2 | Customers BC | 6 | W1 |
| 3 | TASK-W3 | Addresses BC | 5 | W1, W2 |
| 4 | TASK-W4 | API Host + Auth | 5 | W1, W2, W3 |
| 5 | TASK-W5 | Database + Docker | 4 | W4 |
| 6 | TASK-W6 | Integration Tests + Polish | 6 | W4, W5 |
| 7 | TASK-W7 | NATS Fleet Integration | 6 | W4, W5 |

---

## Execution Commands

```bash
/task-work TASK-W1   # Core Foundation
/task-work TASK-W2   # Customers BC
/task-work TASK-W3   # Addresses BC
/task-work TASK-W4   # API Host
/task-work TASK-W5   # Database + Docker
/task-work TASK-W6   # Integration Tests + Polish
/task-work TASK-W7   # NATS Fleet Integration

# After all waves complete:
/template-create
```

---

## Architectural Decisions (Do Not Re-Litigate)

All decisions are final from TASK-59B3 review.

| Decision | Resolution |
|----------|-----------|
| Functional library | CSharpFunctionalExtensions + thin `Result<TError, TSuccess>` wrapper |
| Type ordering | Error-first: `Result<TError, TSuccess>` (not CSharpFunctionalExtensions' success-first) |
| Error hierarchy | `BaseError` abstract record; `StatusCode` virtual, `ErrorCode?` + `InnerException?` init-only |
| Pattern A errors | Enum-discriminated + static factories — used in Customers BC |
| Pattern B errors | Simple per-error records — used in Addresses BC |
| Auth | Keycloak JWT, `MapInboundClaims = false` mandatory, TestContainers in all test environments |
| Database | PostgreSQL 16 + Dapper + Npgsql + DbUp migrations |
| Inter-BC | Addresses depends on `ICustomerLookup` from `Customers.Contracts` only |
| OTEL | Aspire Dashboard (local) + generic OTLP (CI/prod) — no multi-exporter complexity |

---

## Non-Negotiable Rules

1. **No mid-chain `.IsSuccess` checks** — if you find yourself writing `if (result.IsSuccess)` inside a service method between two async operations, use `.BindAsync()` instead
2. **`MapInboundClaims = false`** — must be present in `AuthenticationExtensions.cs`, must have an explanatory comment
3. **Addresses references Customers.Contracts only** — `Exemplar.Addresses.csproj` must not reference `Exemplar.Customers.csproj`
4. **`HandleResultAsync` uses if/else** — not `MatchAsync` (avoids needing a shared return type for both branches)
5. **No LanguageExt** — zero references anywhere in the solution

---

## Template Parameterisation

11 points in the code will be annotated with `// {{TEMPLATE: ...}}` comments in Wave 6, with 4 additional points added in Wave 7, making the solution ready for `/template-create`:

`ProjectName`, `KeycloakRealm`, `KeycloakClientId`, `DatabaseName`, `ServiceName`, `ApiVersion`, `PolicyNames`, `RoutePrefixes`, `DomainName`, `OtelEndpoint`, `MigrationOrder`, `NatsUrl`, `FleetSourceId`, `JetStreamEnabled`, `HeartbeatTimeoutSeconds`

---

## Final State (after Wave 7)

```
Exemplar.sln
src/
  Exemplar.Core/           Result wrapper, BaseError, endpoint extensions, OTEL, Serilog
  Exemplar.Customers/      Domain, service, 4 endpoints (Pattern A errors)
  Exemplar.Customers.Contracts/  ICustomerLookup, CustomerSummaryDto
  Exemplar.Addresses/      Domain, service, 2 endpoints (Pattern B errors)
  Exemplar.API/            Program.cs, auth, health checks
  Exemplar.Fleet/          NATS fleet types, ManifestRegistry, FleetDiscoveryService, NatsAgentClient
db/migrations/             4 SQL files, applied by DbUp
keycloak/                  exemplar-realm.json
tests/
  Exemplar.Core.Tests/
  Exemplar.Customers.Tests/
  Exemplar.Addresses.Tests/
  Exemplar.Fleet.Tests/
docker-compose.yml         PostgreSQL + Keycloak + NATS (JetStream)
Dockerfile
.env.example
README.md
```
