# .NET FastEndpoints Functional Exemplar

## What This Is

Exemplar codebase for the GuardKit `dotnet-fastendpoints` template. Two bounded contexts
(Customers + Addresses) demonstrating functional error handling with
CSharpFunctionalExtensions, railway-oriented composition, and Keycloak JWT auth.

This repo is the input to `/template-create`. All 11 parameterisation points are marked
with `// {{TEMPLATE: ...}}` comments so the generated template can substitute them
automatically.

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Docker + Docker Compose](https://docs.docker.com/get-docker/)

---

## Quick Start

```bash
# 1. Copy environment config
cp .env.example .env
#    Edit .env to set KEYCLOAK_CLIENT_SECRET (and strong passwords for production)

# 2. Start all services
docker compose up

# 3. API is available at http://localhost:5000
#    Swagger UI: http://localhost:5000/swagger
#    Health (liveness):  http://localhost:5000/health/live
#    Health (readiness): http://localhost:5000/health/ready
#    OTEL / Aspire Dashboard: http://localhost:18888
```

---

## Architecture

```
Exemplar.sln
├── src/
│   ├── Exemplar.Core                  — Result<TError,TSuccess>, shared infra, OTEL, health checks
│   ├── Exemplar.Customers             — Customers bounded context (Domain/Application/Infra/Endpoints)
│   ├── Exemplar.Customers.Contracts   — ICustomerLookup inter-BC contract
│   ├── Exemplar.Addresses             — Addresses bounded context
│   └── Exemplar.API                   — Composition root, JWT auth, DbUp migrations, Swagger
└── tests/
    ├── Exemplar.Core.Tests            — Unit tests for Result<TError,TSuccess>
    ├── Exemplar.Customers.Tests       — Unit + integration tests (TestContainers PostgreSQL)
    ├── Exemplar.Addresses.Tests       — Unit + integration tests (TestContainers PostgreSQL)
    └── Exemplar.E2E.Tests             — End-to-end tests (TestContainers PostgreSQL + Keycloak)
```

### Key Patterns

| Pattern | Where | Description |
|---------|-------|-------------|
| **Result&lt;TError, TSuccess&gt;** | `Exemplar.Core.Functional` | Railway-oriented error handling |
| **One class per endpoint** | `*/Endpoints/` | FastEndpoints REPR pattern |
| **Contracts project** | `Exemplar.Customers.Contracts` | Inter-BC communication via interface |
| **DbUp migrations** | `Exemplar.API/Migrations/*.sql` | Schema-as-code, embedded SQL |
| **TestContainers** | `tests/` | Real containers in integration + E2E tests |

### Bounded Contexts

**Customers** (4 endpoints)
- `POST /api/v1/customers` — create (Admin only)
- `GET  /api/v1/customers/{id}` — fetch by ID (authenticated)
- `GET  /api/v1/customers` — list all (authenticated)
- `PUT  /api/v1/customers/{id}/deactivate` — deactivate (Admin only)

**Addresses** (2 endpoints)
- `POST /api/v1/customers/{id}/addresses` — add address (User or Admin)
- `GET  /api/v1/customers/{id}/addresses` — list addresses (authenticated)

### Authentication

Keycloak 26.4 issues JWT tokens. Two realm roles:
- `admin` — full access (CreateCustomer, DeactivateCustomer)
- `user` — standard access (AddAddress, all GET endpoints)

`MapInboundClaims = false` preserves Keycloak claim names (`realm_access.roles`).
Role mapping from Keycloak's JWT payload to `ClaimTypes.Role` is handled in
`AuthenticationExtensions.ProcessKeycloakRoles`.

---

## Running Tests

```bash
# All tests (unit + integration + E2E)
dotnet test

# Unit + integration only (no Docker required for the API fixture tests)
dotnet test tests/Exemplar.Core.Tests/ tests/Exemplar.Customers.Tests/ tests/Exemplar.Addresses.Tests/

# E2E only (requires Docker)
dotnet test tests/Exemplar.E2E.Tests/
```

E2E tests start ephemeral PostgreSQL and Keycloak containers via TestContainers,
run the full application (including DbUp migrations), and test all 6 endpoints with
real JWTs obtained from the Keycloak TestContainer.

---

## Template Parameterisation Points

The 11 `// {{TEMPLATE: ...}}` markers tell `/template-create` what to substitute:

| # | Marker | Example value |
|---|--------|---------------|
| 1 | `ProjectName` | `Exemplar` — replaces all namespace prefixes |
| 2 | `KeycloakRealm` | `exemplar` — Keycloak realm name |
| 3 | `KeycloakClientId` | `exemplar-api` — OAuth2 client ID |
| 4 | `DatabaseName` | `exemplar` — PostgreSQL database name |
| 5 | `ServiceName` | `api` — Docker Compose service name |
| 6 | `ApiVersion` | `v1` — URL path version segment |
| 7 | `PolicyNames` | `admin`/`user` — Keycloak realm role names |
| 8 | `RoutePrefixes` | `customers`/`addresses` — API route prefixes |
| 9 | `DomainName` | `Customer`/`Address` — bounded context entity name |
| 10 | `OtelEndpoint` | OTLP collector URL |
| 11 | `MigrationOrder` | `0001_` — migration file numbering prefix |

---

## Extending This Template

**Add a message bus**
- NATS JetStream: add `NATS.Client.JetStream`, publish domain events from services
- MassTransit: register `AddMassTransit()` in Program.cs, keep services unchanged

**Switch to EF Core**
- Replace Dapper repositories with EF Core DbContext
- Keep service/endpoint layers unchanged (they depend on `ICustomerRepository`)
- Remove DbUp migrations, use EF Core migrations instead

**Add more bounded contexts**
- Follow the `Customers.Contracts` inter-BC pattern:
  1. Create a `YourBC.Contracts` project with the contract interface
  2. Implement it in your service and register as both `IYourService` and `IContractInterface`
  3. Reference `YourBC.Contracts` from consuming BCs only

**Add a second API version**
- Change the `api/v1/...` route prefix (`// {{TEMPLATE: ApiVersion}}`)
- Register a new endpoint assembly in `Program.cs`
