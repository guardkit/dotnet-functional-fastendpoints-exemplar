---
id: TASK-W3
title: "Addresses BC — domain, repository, service, 2 endpoints, inter-BC contract"
status: completed
task_type: feature
wave: 3
created: 2026-04-10T00:00:00Z
updated: 2026-04-10T00:00:00Z
completed: 2026-04-10T00:00:00Z
priority: high
complexity: 5
parent_review: TASK-59B3
feature_id: dotnet-fastendpoints-exemplar
dependencies: [TASK-W1, TASK-W2]
tags: [dotnet, fastendpoints, addresses, bounded-context, inter-bc]
previous_state: in_review
completed_location: tasks/completed/TASK-W3/
---

# TASK-W3: Addresses Bounded Context

## Scope

Implement the Addresses bounded context. This BC demonstrates:
- **Pattern B** error handling: simple per-error records
- **Inter-BC communication**: `AddressService` depends on `ICustomerLookup` (from `Customers.Contracts`) — never on `CustomerService` directly

## Deliverables

| # | Task | Output |
|---|------|--------|
| 22 | `Address.cs` entity + `AddressError` records — Pattern B | Domain layer |
| 23 | `IAddressService.cs` in `Addresses.Contracts/` | Public API surface |
| 24 | `IAddressRepository.cs` + `AddressRepository.cs` (Dapper) | Data access |
| 25 | `AddressService.cs` — calls `ICustomerLookup` via `.BindAsync()` before insert | Inter-BC query in chain |
| 26 | `GetAddressesByCustomerId.cs` — `GET /api/v1/customers/{id}/addresses` `RequireAuthentication` | Endpoint |
| 27 | `AddAddress.cs` — `POST /api/v1/customers/{id}/addresses` `UserOrAdmin` | Endpoint |
| 28 | Unit tests — all paths; integration test — unknown customer returns 404 | Tests |

## Project Structure

```
src/Exemplar.Addresses/
  Domain/
    Address.cs
    Errors/
      AddressNotFoundError.cs
      CustomerNotFoundError.cs
      DuplicatePrimaryAddressError.cs
  Application/
    IAddressService.cs
    AddressService.cs
  Infrastructure/
    IAddressRepository.cs
    AddressRepository.cs
  Endpoints/
    GetAddressesByCustomerId.cs
    AddAddress.cs

tests/Exemplar.Addresses.Tests/
  Unit/
    AddressServiceTests.cs
  IntegrationTests/
    AddressEndpointsTests.cs
```

## Quality Gates

- [x] `dotnet build` — zero warnings, zero errors
- [x] `dotnet test Exemplar.Addresses.Tests` — 8/8 unit tests pass
- [x] `Exemplar.Addresses.csproj` references `Exemplar.Customers.Contracts` only — not `Exemplar.Customers`
- [x] Integration test: `POST /customers/{unknownId}/addresses` returns 404
- [x] Integration test: adding second primary address returns 409

## Completion Notes

### Key Design Decisions
- `AddressService` uses `ICustomerLookup` (inter-BC contract) — never `CustomerService` directly
- `MapErrorAsync` added to `ResultExtensions` to adapt `Result<NotFoundError, ...>` → `Result<BaseError, ...>` across BC boundary
- `BaseError.ErrorCode` made virtual to support Pattern B error records with per-type error codes
- `CustomerError` (Pattern A) updated to derive `ErrorCode` from `Kind` via switch expression — cleaner than init syntax
- Integration tests seed customers via SQL (no cross-BC HTTP dependency in test fixture)

## Next Wave

TASK-W4 (API Host) wires both BCs into `Program.cs` and adds auth.
