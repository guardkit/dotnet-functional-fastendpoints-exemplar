# Feature: .NET FastEndpoints Functional Exemplar

**Status**: Backlog  
**Parent review**: [TASK-59B3 review report](../../../.claude/reviews/TASK-59B3-review-report.md)

## Problem Statement

The GuardKit `dotnet-fastendpoints` built-in template does not yet exist. To generate it via `/template-create`, a canonical working solution is needed that demonstrates all the architectural patterns the template must encode.

## Solution

Build a minimal but complete `.NET 8` solution using two generic bounded contexts (Customers + Addresses) that together demonstrate every pattern the template must include:

- `Result<TError, TSuccess>` wrapper over CSharpFunctionalExtensions (error-first ordering)
- `BaseError` abstract record hierarchy with two error styles (enum-discriminated + simple records)
- Railway-oriented `.Bind()/.BindAsync()/.Map()` chains throughout the service layer
- FastEndpoints one-class-per-endpoint pattern with `HandleResultAsync` bridge
- Keycloak JWT auth with `MapInboundClaims = false`
- Inter-BC communication via a `Contracts` project interface
- PostgreSQL + Dapper + DbUp migrations
- Docker Compose local dev stack
- TestContainers integration tests (real PostgreSQL + real Keycloak)

## Subtasks

| Wave | Task | Status |
|------|------|--------|
| 1 | [TASK-W1 — Core Foundation](TASK-W1-core-foundation.md) | Backlog |
| 2 | [TASK-W2 — Customers BC](TASK-W2-customers-bc.md) | Backlog |
| 3 | [TASK-W3 — Addresses BC](TASK-W3-addresses-bc.md) | Backlog |
| 4 | [TASK-W4 — API Host + Auth](TASK-W4-api-host.md) | Backlog |
| 5 | [TASK-W5 — Database + Docker](TASK-W5-database-docker.md) | Backlog |
| 6 | [TASK-W6 — Integration Tests + Polish](TASK-W6-integration-tests-polish.md) | Backlog |
| 7 | [TASK-W7 — NATS Fleet Integration](TASK-W7-nats-fleet-integration.md) | Backlog |

See [IMPLEMENTATION-GUIDE.md](IMPLEMENTATION-GUIDE.md) for execution strategy and non-negotiable rules.

> **W7 extends the exemplar** with NATS client integration — capability discovery via `AgentManifest`, domain event publishing via JetStream, and request-reply with AI agents using the `Result<TError, TSuccess>` pattern. This is how the FinProxy LPA .NET platform interfaces with Python AI agents in the fleet.
