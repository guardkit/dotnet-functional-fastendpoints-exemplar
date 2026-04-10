---
id: TASK-W6
title: "Integration Tests + Polish — TestContainers Keycloak, E2E, template annotations, README"
status: backlog
task_type: feature
wave: 6
created: 2026-04-10T00:00:00Z
updated: 2026-04-10T00:00:00Z
priority: high
complexity: 6
parent_review: TASK-59B3
feature_id: dotnet-fastendpoints-exemplar
dependencies: [TASK-W4, TASK-W5]
tags: [dotnet, testcontainers, keycloak, integration-tests, template-create]
---

# TASK-W6: Integration Tests + Polish

## Scope

Full integration test coverage with real PostgreSQL + Keycloak via TestContainers, end-to-end smoke test, template parameterisation annotations, and the repo README.

## Deliverables

| # | Task | Output |
|---|------|--------|
| 40 | Keycloak TestContainers setup — real container, real JWT acquisition | Auth fixture |
| 41 | E2E: obtain JWT → call all 6 endpoints → verify status codes + bodies | Complete coverage |
| 42 | Template annotation pass — inline comments marking the 11 parameterisation points | `/template-create` input ready |
| 43 | `README.md` — getting started, architecture overview, extension points | Developer guide |

## TestContainers Setup

### Fixtures (shared across all integration tests)

```csharp
// tests/Exemplar.Tests.Shared/Fixtures/PostgreSqlFixture.cs
public class PostgreSqlFixture : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("exemplar_test")
            .WithUsername("exemplar")
            .WithPassword("test_password")
            .Build();
        await _container.StartAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}

// tests/Exemplar.Tests.Shared/Fixtures/KeycloakFixture.cs
public class KeycloakFixture : IAsyncLifetime
{
    private KeycloakContainer _container = null!;
    public string AuthorityUrl => $"{_container.GetBaseAddress()}/realms/exemplar";

    public async Task InitializeAsync()
    {
        _container = new KeycloakBuilder()
            .WithImage("quay.io/keycloak/keycloak:26.4")
            .WithResourceMapping("exemplar-realm.json", "/opt/keycloak/data/import/")
            .Build();
        await _container.StartAsync();
    }

    public async Task<string> GetTokenAsync(string username, string password)
    {
        // Token endpoint call using HttpClient
        // Returns Bearer token for use in test requests
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}
```

### Integration Test — Customer endpoints
```csharp
[Collection("IntegrationTests")]
public class CustomerEndpointsTests(AppFactory factory) : IClassFixture<AppFactory>
{
    [Fact]
    public async Task CreateCustomer_AdminToken_Returns201()
    {
        var token = await _keycloak.GetTokenAsync("admin_user", "password");
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/v1/customers", new
        {
            name  = "Acme Corp",
            email = "contact@acme.com"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateCustomer_UserToken_Returns403()
    {
        // Non-admin role → AdminOnly policy → 403
    }

    [Fact]
    public async Task GetCustomer_UnknownId_Returns404()
    {
        // CustomerError.NotFound → 404 with ProblemDetails
    }
}
```

## Template Annotation Pass (Task 42)

Add `// {{TEMPLATE: ...}}` comments at all 11 parameterisation points:

| # | Location | Annotation |
|---|----------|-----------|
| 1 | Every `namespace Exemplar.*` | `// {{TEMPLATE: ProjectName}}` |
| 2 | `exemplar` realm name in config | `// {{TEMPLATE: KeycloakRealm}}` |
| 3 | `exemplar-api` client ID | `// {{TEMPLATE: KeycloakClientId}}` |
| 4 | `exemplar_db` database name | `// {{TEMPLATE: DatabaseName}}` |
| 5 | `exemplar-api` Docker service name | `// {{TEMPLATE: ServiceName}}` |
| 6 | `v1` API version prefix | `// {{TEMPLATE: ApiVersion}}` |
| 7 | `AdminOnly` / `UserOrAdmin` policy names | `// {{TEMPLATE: PolicyNames}}` |
| 8 | `customers` / `addresses` route prefixes | `// {{TEMPLATE: RoutePrefixes}}` |
| 9 | `CustomerError` / `AddressError` type names | `// {{TEMPLATE: DomainName}}Error` |
| 10 | OTLP endpoint URL | `// {{TEMPLATE: OtelEndpoint}}` |
| 11 | Migration file prefix `0001_` | `// {{TEMPLATE: MigrationOrder}}` |

## README.md Structure

```markdown
# .NET FastEndpoints Functional Exemplar

## What This Is
Exemplar for the GuardKit `dotnet-fastendpoints` template. Two bounded contexts
(Customers + Addresses) demonstrating functional error handling with
CSharpFunctionalExtensions, railway-oriented composition, and Keycloak JWT auth.

## Prerequisites
- .NET 8 SDK
- Docker + Docker Compose

## Quick Start
cp .env.example .env
docker compose up

## Architecture
- Modular monolith — two bounded contexts in one deployable
- CSharpFunctionalExtensions Result<TError,TSuccess> — error-first wrapper
- FastEndpoints — one class per endpoint
- Keycloak 26.4 — JWT auth, MapInboundClaims = false
- PostgreSQL 16 + Dapper + DbUp

## Extending This Template
- Add a message bus: NATS JetStream or MassTransit
- Switch to EF Core: replace Dapper repositories, keep service/endpoint layers unchanged
- Add more BCs: follow the Customers.Contracts inter-BC pattern
```

## Quality Gates

- [ ] All integration tests pass against real PostgreSQL + Keycloak containers
- [ ] E2E: all 6 endpoints return expected status codes with valid JWT
- [ ] E2E: all 6 endpoints return 401 with no token
- [ ] E2E: `AdminOnly` endpoints return 403 with user-role token
- [ ] All 11 `// {{TEMPLATE: ...}}` annotations present and correctly placed
- [ ] `docker compose up` → `README.md` quick start steps work end-to-end
- [ ] Solution ready for `/template-create`

## After This Wave

```bash
/template-create
```
