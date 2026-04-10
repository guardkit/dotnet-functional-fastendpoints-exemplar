---
id: TASK-59B3
title: Review and plan .NET FastEndpoints functional exemplar for GuardKit template
status: review_complete
task_type: review
created: 2026-04-09T00:00:00Z
updated: 2026-04-09T00:00:00Z
priority: high
tags: [dotnet, fastendpoints, functional-error-handling, guardkit, template-create, review]
complexity: 7
decision_required: true
review_results:
  mode: decision
  depth: deep
  score: 91
  findings_count: 4
  recommendations_count: 6
  decision: implement
  report_path: .claude/reviews/TASK-59B3-review-report.md
  completed_at: 2026-04-09T00:00:00Z
  implementation_feature: tasks/backlog/dotnet-fastendpoints-exemplar/
---

# Task: Review and Plan .NET FastEndpoints Functional Exemplar for GuardKit Template

## Purpose

Review all reference material, existing exemplar repos, and best practices in order to produce a detailed implementation plan for building a `.NET FastEndpoints + Functional Error Handling` exemplar in this repo. The exemplar will serve as the canonical input to `/template-create` to generate a new GuardKit built-in template called `dotnet-fastendpoints`.

Use `/task-review TASK-59B3` to execute this task. After completion, create an implementation task from the findings.

---

## Background

### Why This Exemplar Exists

The FinProxy platform (LPA financial monitoring) has adopted:
- **FastEndpoints** (not MVC controllers) — one endpoint class per use case
- **CSharpFunctionalExtensions Result\<T,E\>** wrapped in a thin `FinProxy.Core.Result` adapter — functional error handling without LanguageExt's complexity
- **Modular monolith** — per-bounded-context project boundaries within a single deployable
- **Keycloak JWT** — with FAPI 2.0 and custom claim mapping
- **OpenTelemetry + Serilog** — observability-first from ground up

The exemplar should reflect the FinProxy direction: CSharpFunctionalExtensions + thin wrapper + BaseError hierarchy.

This exemplar will be the first-class reference for the GuardKit `dotnet-fastendpoints` template and should be rich enough to generate high-quality scaffolding.

---

## Primary Reference Documents

| Document | Location | Key Content |
|----------|----------|-------------|
| FinProxy DotNet API Patterns & Topology | `/Users/richardwoollcott/Projects/appmilla_github/lpa-platform/docs/architecture/FinProxy-DotNet-API-Patterns-And-Topology.md` | FastEndpoints conventions, modular monolith topology, .NET solution structure, BC module boundaries, extraction criteria |
| FinProxy Functional Error Handling Evaluation | `/Users/richardwoollcott/Projects/appmilla_github/lpa-platform/docs/architecture/FinProxy-Functional-Error-Handling-Evaluation.md` | LanguageExt vs ErrorOr vs OneOf vs FluentResults vs CSharpFunctionalExtensions. C# 15 native unions (Nov 2026). **Decision: CSharpFunctionalExtensions + thin wrapper** |
| FinProxy Deployment Strategy Research | `/Users/richardwoollcott/Projects/appmilla_github/lpa-platform/docs/architecture/FinProxy-Deployment-Strategy-Research.md` | Docker Compose (local) → ECS/Fargate (cloud). Service topology. Copilot CLI EOL. Cost estimates |
| FinProxy Conversation Starter | `/Users/richardwoollcott/Projects/appmilla_github/lpa-platform/docs/architecture/finproxy-conversation-starter.md` | Full DDD context, bounded context definitions, technology decisions, constraints, preferred directions |
| FinProxy Keycloak Configuration Research | `/Users/richardwoollcott/Projects/appmilla_github/lpa-platform/docs/architecture/FinProxy-Keycloak-Configuration-Research.md` | KC 26.4+ FAPI 2.0, single realm + Organizations for B2B, .NET OIDC integration (MapInboundClaims=false, Audience mapper), client config, pitfalls to avoid |

---

## Key Architectural Decision: CSharpFunctionalExtensions

FinProxy has evaluated alternatives and chosen **CSharpFunctionalExtensions** (`Result<TSuccess, TError>`). The review must validate this choice and determine the exact wrapper API for the exemplar.

### FinProxy Evaluation Summary (from Functional-Error-Handling-Evaluation.md)

| Library | Verdict | Reason |
|---------|---------|--------|
| LanguageExt | ❌ Avoid | Excellent but AI-unfriendly; `Either<L,R>` convention (Left=error) confuses AI code generation; complex HKT/LINQ magic produces brittle AI output |
| ErrorOr | ❌ Avoid | Stringly-typed error messages; no proper error hierarchy; poor monadic composition |
| OneOf | ❌ Avoid | Good for discriminated unions but no monadic bind; requires manual unwrapping throughout |
| FluentResults | ❌ Avoid | Not monadic; can't chain operations with `.Bind()`; errors not typed |
| **CSharpFunctionalExtensions** | ✅ **Chosen** | `Result<TSuccess, TError>` — right-biased (Success is right, natural); typed errors; `.Bind()`, `.Map()`, `.Match()` — monadic; AI-friendly naming |

### Migration Convention for the Exemplar

The exemplar should use the FinProxy wrapper convention:
```csharp
// Thin wrapper namespace (migration-ready for C# 15 native unions, Nov 2026)
namespace Exemplar.Core.Functional;

// Service signatures use CSharpFunctionalExtensions directly OR thin alias
// Either convention: Result<TSuccess, TError> where TError : BaseError
public Task<Result<ProductDto, ProductError>> GetProductAsync(int id, CancellationToken ct);

// Endpoint extension mirrors HandleEitherResultAsync but for Result<T,E>
public static async Task HandleResultAsync<TReq, TRes, TSuccess, TError>(
    this Endpoint<TReq, TRes> endpoint,
    Result<TSuccess, TError> result,
    Func<TSuccess, Task> successHandler,
    CancellationToken ct
) where TError : BaseError
```

---

## What the Exemplar Must Demonstrate

The review should confirm and refine these requirements. The exemplar is a **working .NET solution** — not just docs — with enough real code to drive `/template-create`.

### 1. Solution Structure (Modular Monolith)
```
Exemplar.sln
├── src/
│   ├── Exemplar.Core/                    # Shared infrastructure (NuGet-style)
│   │   ├── Functional/
│   │   │   ├── BaseError.cs              # Abstract record, StatusCode, ErrorCode
│   │   │   ├── ResultExtensions.cs       # HandleResultAsync endpoint extension
│   │   │   └── ProblemDetailsMapper.cs   # BaseError → ProblemDetails
│   │   ├── FastEndpoints/
│   │   │   ├── FastEndpointsExtensions.cs
│   │   │   └── CustomSchemaNameGenerator.cs
│   │   ├── Observability/
│   │   │   ├── OpenTelemetryExtensions.cs
│   │   │   └── StructuredLoggingExtensions.cs
│   │   └── Health/
│   │       └── HealthCheckExtensions.cs
│   ├── Exemplar.Catalog/                 # Bounded context: Product Catalog
│   │   ├── Contracts/                    # Public API surface (no internal refs from other BCs)
│   │   │   └── ICatalogService.cs
│   │   ├── Domain/
│   │   │   ├── Errors/
│   │   │   │   └── CatalogError.cs       # Enum-based error record : BaseError
│   │   │   └── Entities/
│   │   │       └── Product.cs
│   │   ├── Endpoints/
│   │   │   ├── Products/
│   │   │   │   ├── GetProductById.cs     # GET /catalog/api/v1/products/{id}
│   │   │   │   ├── GetAllProducts.cs     # GET /catalog/api/v1/products
│   │   │   │   └── CreateProduct.cs      # POST /catalog/api/v1/products
│   │   ├── Services/
│   │   │   └── CatalogService.cs         # Result<T,E> composition
│   │   └── Repositories/
│   │       └── ProductRepository.cs
│   └── Exemplar.API/                     # Host / entry point
│       ├── Program.cs
│       └── appsettings.json
└── tests/
    ├── Exemplar.Core.Tests/
    └── Exemplar.Catalog.Tests/
```

### 2. Error Handling Pattern (CSharpFunctionalExtensions)
- `BaseError` abstract record with `StatusCode`, optional `ErrorCode` string
- Domain-specific errors: `CatalogError` with enum discriminant + `Exception?`
- Simple cross-cutting errors: `NotFoundError`, `ValidationError`, `UnauthorizedError` : `BaseError`
- `ResultExtensions.HandleResultAsync` extension on `Endpoint<TReq,TRes>`
- RFC 7807 ProblemDetails builder using `BaseError.StatusCode`
- **No exceptions for expected errors** — only for truly unexpected/infrastructure failures

### 3. FastEndpoints Conventions
- One class per endpoint, organised by resource subfolder under `Endpoints/`
- Request/Response record pairs co-located with endpoint or in `Models/` subfolder
- FluentValidation validators in `Validators/` subfolder
- `Endpoint<TRequest, TResponse>` base (not minimal API style)
- Auth: `[Authorize]` attribute or `Options.Roles(...)` in `Configure()`
- Route prefix: `{bcName}/api/{version}`
- OpenAPI doc group per bounded context

### 4. Service/Repository Layer
- Services return `Task<Result<TSuccess, TError>>` — never throw for expected errors
- Repositories return `Task<Result<TSuccess, TError>>` — wrap DB exceptions in `InfrastructureError`
- Monadic composition with `.Bind()`, `.Map()`, `.Match()`
- Interface-backed, DI-registered

### 5. Keycloak JWT Auth ✅ DECIDED: Full Keycloak in all environments
- **Decision:** Full Keycloak JWT in local dev, CI, and production — no stubs or simplified JWT-only modes
- **Rationale:** Environment parity is a hard requirement. Auth bugs that only surface in higher environments (staging/prod) are expensive to debug and may indicate security issues. Keycloak runs in Docker Compose for local dev using the same config as production.
- JWT Bearer with `MapInboundClaims = false`
- Custom claim processor: `realm_access.roles` + `resource_access.{client}.roles` → `ClaimTypes.Role`
- Audience mapper configuration
- Authorization policies: `AdminOnly`, `UserOrAdmin`
- Keycloak 26.4+ in `docker-compose.yml` with realm export/import for reproducible local setup
- Keycloak health check (API waits for KC to be healthy before accepting traffic)
- Config: KC 26.4+ FAPI 2.0 Final client profile settings
- Realm config committed to repo as JSON export — same realm used in all environments

### 6. Observability
- OpenTelemetry: traces + metrics + logs (ASP.NET Core + HttpClient instrumentation)
- Serilog: structured logging, enriched with correlation ID, user ID, request path
- `StructuredLoggingExtensions`: `LogApiError<TError>()`, `LogAggregatedError()`
- OTLP exporter config (Aspire Dashboard / Jaeger local dev)
- Health checks: liveness + readiness probes

### 7. Docker Compose (Local Dev)
- `docker-compose.yml` with:
  - API service
  - PostgreSQL
  - Keycloak 26.4+ (FAPI config)
  - OTEL Collector / Aspire Dashboard
- `.env.example` with all required vars
- Health check dependencies

### 8. Tests
- Unit tests: service layer with mocked repository (Result<T,E> assertions)
- Integration tests: FastEndpoints `App.CreateClient()` pattern, real endpoint tests
- Error scenario tests: verify correct HTTP status codes from `BaseError.StatusCode`
- FluentAssertions + xUnit

---

## Review Objectives

Execute `/task-review TASK-59B3` to analyse and produce decisions on:

### Decision 1: CSharpFunctionalExtensions Wrapper API Design
- Should the exemplar use `CSharpFunctionalExtensions.Result<TSuccess, TError>` directly or wrap it?
- What should the `HandleResultAsync` extension signature look like?
- Should the wrapper use type aliases or thin wrapper records?
- How to handle the C# 15 native unions migration path (Nov 2026)?

### Decision 2: BaseError Hierarchy
- What error types should the Core library provide out-of-the-box?
- Should `BaseError` include `ErrorCode` (string) alongside `StatusCode` (int)?
- Enum-based vs simple record-based for domain errors — when to use each?
- Should `InfrastructureError` wrap raw exceptions separately from domain errors?

### Decision 3: Bounded Context Scope for Exemplar
- What is the right domain for the exemplar? (Product Catalog is proposed — simple CRUD + a richer workflow)
- Should it include a second bounded context to demonstrate inter-BC communication?
- Does it need a message bus consumer (MassTransit/NATS) to be useful for the FinProxy template?

### Decision 4: Auth Complexity ✅ RESOLVED
**Decision:** Full Keycloak JWT in all environments (local dev, CI, production). No stubs, no simplified JWT-only mode.
**Rationale:** Environment parity is a hard requirement — auth issues that only appear in higher environments are expensive to debug and may mask security problems. Keycloak runs in Docker Compose locally with the same FAPI 2.0 config used in production. Realm config is committed to the repo as a JSON export for reproducible setup.
**Remaining questions for review:** Which policies to demonstrate beyond `AdminOnly` / `UserOrAdmin`?

### Decision 5: Database
- PostgreSQL with Dapper or EF Core?
- Include migrations tooling (Flyway, DbUp, EF Migrations)?
- Should the exemplar use an in-memory repository for simplicity and full DB for integration tests?

### Decision 6: Template-Create Readiness
- What is the minimum viable exemplar to produce a useful GuardKit template?
- Which features are "must have" vs "nice to have" for the template?
- What parameterisable placeholders will the template need (project name, namespace, BC name, etc.)?

---

## Research References (Best Practices & Community Examples)

### FastEndpoints
- Official docs: https://fast-endpoints.com
- GitHub: https://github.com/FastEndpoints/FastEndpoints
- Template: `dotnet new FastEndpoints.Template`
- Community samples: https://github.com/FastEndpoints/Template-Pack

### CSharpFunctionalExtensions
- GitHub: https://github.com/vkhorikov/CSharpFunctionalExtensions
- Blog series: Vladimir Khorikov "Applying functional principles in C#"
- NuGet: `CSharpFunctionalExtensions` (v3.x for .NET 8+)

### Modular Monolith .NET
- Modular Monolith with DDD (Oskar Dudycz): https://github.com/oskardudycz/Wolverine-CQRS-Sample
- Milan Jovanovic modular monolith series
- Steve Smith / Ardalis modular monolith approach

### .NET + Keycloak FAPI 2.0
- KC 26.4 release notes (FAPI 2.0 Final profiles)
- `MapInboundClaims = false` requirement (Keycloak 24+ breaking change)
- Organisations feature for multi-tenant B2B

### OpenTelemetry .NET
- `OpenTelemetry.Extensions.Hosting` + `OpenTelemetry.Instrumentation.AspNetCore`
- Aspire Dashboard as local OTEL collector

---

## Acceptance Criteria

- [ ] All 6 decisions above are resolved with clear rationale
- [ ] Implementation plan produced: ordered list of tasks to build the exemplar
- [ ] Solution structure diagram finalised (may differ from proposal above)
- [ ] CSharpFunctionalExtensions wrapper API design documented with code examples
- [ ] BaseError hierarchy documented with all base error types
- [ ] Docker Compose service list confirmed
- [ ] Test strategy confirmed (unit + integration approach)
- [ ] GuardKit template parameterisation plan produced (what will be templated vs hardcoded)
- [ ] Risks and open questions identified

---

## Test Requirements

This is a review task — no tests to write. The deliverable is a decision document and implementation plan that will seed the next task (`/task-work` on the implementation task created from this review).

---

## Implementation Notes

**Suggested workflow after review completion:**

1. Run `/task-review TASK-59B3` — produces decision document + ordered implementation plan
2. Create implementation task: `/task-create "Implement .NET FastEndpoints functional exemplar" task_type:feature`
3. Work the implementation task: `/task-work TASK-XXXX`
4. Once exemplar is complete and working, run `/template-create` against this repo

**Key constraint:** The exemplar must be a *working, runnable* .NET solution — not just a code snippet collection. It needs to demonstrate all patterns end-to-end, including a full HTTP request cycle, error propagation, auth, and observability. This is what makes `/template-create` produce a useful template.

**Migration path note:** The FinProxy architecture anticipates migrating from CSharpFunctionalExtensions to C# 15 native discriminated unions (November 2026). The wrapper design chosen in Decision 1 must make this migration a single-file change in `Exemplar.Core`, not a cross-cutting refactor.

---

## Source Material Summary

| Source | Type | Key Contribution |
|--------|------|-----------------|
| `FinProxy-DotNet-API-Patterns-And-Topology.md` | Architecture doc | FastEndpoints conventions, modular monolith topology, solution structure |
| `FinProxy-Functional-Error-Handling-Evaluation.md` | Evaluation doc | Library comparison, CSharpFunctionalExtensions decision, C# 15 migration plan |
| `FinProxy-Deployment-Strategy-Research.md` | Research doc | Docker Compose → ECS/Fargate pattern, local dev setup |
| `finproxy-conversation-starter.md` | Architecture synthesis | Full DDD constraints, preferred technology directions, regulatory context |
| `FinProxy-Keycloak-Configuration-Research.md` | Research doc | KC 26.4+ FAPI 2.0 config, .NET integration pitfalls, MapInboundClaims |

## Test Execution Log

_Populated by /task-review_
