---
id: TASK-W3
title: "Addresses BC — domain, repository, service, 2 endpoints, inter-BC contract"
status: backlog
task_type: feature
wave: 3
created: 2026-04-10T00:00:00Z
updated: 2026-04-10T00:00:00Z
priority: high
complexity: 5
parent_review: TASK-59B3
feature_id: dotnet-fastendpoints-exemplar
dependencies: [TASK-W1, TASK-W2]
tags: [dotnet, fastendpoints, addresses, bounded-context, inter-bc]
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

## Key Implementation Rules

### AddressError — Pattern B (simple records, each overrides StatusCode)
```csharp
// No enum — each error is its own record type
public record AddressNotFoundError(Guid AddressId)
    : BaseError($"Address {AddressId} not found")
{
    public override int StatusCode => StatusCodes.Status404NotFound;
    public override string? ErrorCode => "ADDRESS_NOT_FOUND";
}

public record CustomerNotFoundError(Guid CustomerId)
    : BaseError($"Customer {CustomerId} not found")
{
    public override int StatusCode => StatusCodes.Status404NotFound;
    public override string? ErrorCode => "ADDRESS_CUSTOMER_NOT_FOUND";
}

public record DuplicatePrimaryAddressError(Guid CustomerId)
    : BaseError($"Customer {CustomerId} already has a primary address")
{
    public override int StatusCode => StatusCodes.Status409Conflict;
    public override string? ErrorCode => "ADDRESS_DUPLICATE_PRIMARY";
}
```

### AddressService — ICustomerLookup dependency (NOT CustomerService)
```csharp
public class AddressService : IAddressService
{
    // Depends on the contract interface, not the Customers BC internals
    public AddressService(ICustomerLookup customerLookup, IAddressRepository repo) { ... }

    public async Task<Result<BaseError, AddressDto>> AddAddressAsync(
        Guid customerId, AddAddressRequest request, CancellationToken ct)
    {
        return await _customerLookup.FindByIdAsync(customerId, ct)
            .MapError(e => (BaseError)new CustomerNotFoundError(customerId))
            .BindAsync(customer => ValidateNoPrimaryConflict(customer, request, ct))
            .BindAsync(validated => _repo.InsertAsync(Address.Create(customerId, request), ct))
            .Map(address => address.ToDto());
    }
}
```

### Project Reference Rule — Addresses.csproj
```xml
<!-- Correct: reference only the Contracts project -->
<ProjectReference Include="..\Exemplar.Customers.Contracts\Exemplar.Customers.Contracts.csproj" />

<!-- WRONG: never reference Customers internals -->
<!-- <ProjectReference Include="..\Exemplar.Customers\Exemplar.Customers.csproj" /> -->
```

## Quality Gates

- [ ] `dotnet build` — zero warnings, zero errors
- [ ] `dotnet test Exemplar.Addresses.Tests` — 100% pass, ≥80% line coverage
- [ ] `Exemplar.Addresses.csproj` references `Exemplar.Customers.Contracts` only — not `Exemplar.Customers`
- [ ] Integration test: `POST /customers/{unknownId}/addresses` returns 404
- [ ] Integration test: adding second primary address returns 409

## Next Wave

TASK-W4 (API Host) wires both BCs into `Program.cs` and adds auth.
