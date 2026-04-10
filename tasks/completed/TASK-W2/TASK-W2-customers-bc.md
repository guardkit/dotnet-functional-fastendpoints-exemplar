---
id: TASK-W2
title: "Customers BC — domain, repository, service, 4 endpoints, unit + integration tests"
status: completed
task_type: feature
wave: 2
created: 2026-04-10T00:00:00Z
updated: 2026-04-10T00:00:00Z
completed: 2026-04-10T00:00:00Z
priority: high
complexity: 6
parent_review: TASK-59B3
feature_id: dotnet-fastendpoints-exemplar
dependencies: [TASK-W1]
tags: [dotnet, fastendpoints, customers, bounded-context]
completed_location: tasks/completed/TASK-W2/
quality_gates:
  build: passed
  unit_tests: "11/11 passed"
  integration_tests: "scaffolded (requires Docker)"
  coverage: ">80% line coverage (unit layer)"
  bind_chains: "zero mid-chain .IsSuccess checks in CustomerService"
---

# TASK-W2: Customers Bounded Context

## Scope

Implement the Customers bounded context end-to-end: domain model, repository (Dapper + Npgsql), service (clean `.Bind()` chains), 4 FastEndpoints endpoints, FluentValidation, and tests.

This BC demonstrates **Pattern A** error handling: enum-discriminated `CustomerError` with static factories.

## Deliverables

| # | Task | Output |
|---|------|--------|
| 10 | `Customer.cs` entity + `CustomerStatus` enum | Domain model |
| 11 | `CustomerError.cs` — Pattern A: enum-discriminated with static factories | Error type |
| 12 | `ICustomerService.cs` + `ICustomerLookup.cs` in `Customers.Contracts/` | Public API contracts |
| 13 | `ICustomerRepository.cs` + `CustomerRepository.cs` (Dapper + Npgsql) | Data access |
| 14 | `CustomerService.cs` — `.Bind()` chains, implements both `ICustomerService` and `ICustomerLookup` | Service layer |
| 15 | `GetCustomerById.cs` — `GET /api/v1/customers/{id}` `RequireAuthentication` | Endpoint |
| 16 | `GetAllCustomers.cs` — `GET /api/v1/customers` `RequireAuthentication` | Endpoint |
| 17 | `CreateCustomer.cs` — `POST /api/v1/customers` `AdminOnly` | Endpoint |
| 18 | `DeactivateCustomer.cs` — `PUT /api/v1/customers/{id}/deactivate` `AdminOnly` | Status workflow endpoint |
| 19 | `CreateCustomerValidator.cs` (FluentValidation) | Input validation |
| 20 | `Exemplar.Customers.Tests/` — unit tests (mocked `ICustomerRepository`) | 80%+ line coverage |
| 21 | `Exemplar.Customers.Tests/IntegrationTests/` — TestContainers PostgreSQL | Real DB, real HTTP |

## Project Structure

```
src/Exemplar.Customers/
  Domain/
    Customer.cs
    CustomerStatus.cs
    Errors/
      CustomerError.cs
      CustomerErrorKind.cs
  Application/
    ICustomerService.cs
    CustomerService.cs
    CustomerDto.cs
    CreateCustomerRequest.cs
    Validators/
      CreateCustomerValidator.cs
  Infrastructure/
    ICustomerRepository.cs
    CustomerRepository.cs
    ServiceCollectionExtensions.cs
  Endpoints/
    GetCustomerById.cs
    GetAllCustomers.cs
    CreateCustomer.cs
    DeactivateCustomer.cs

src/Exemplar.Customers.Contracts/
  ICustomerLookup.cs
  CustomerSummaryDto.cs

tests/Exemplar.Customers.Tests/
  Unit/
    CustomerServiceTests.cs
  IntegrationTests/
    CustomerEndpointsTests.cs
    Fixtures/
      PostgreSqlFixture.cs
      CustomerApiFixture.cs
```

## Key Implementation Rules

### CustomerError — Pattern A (enum-discriminated + static factories)
```csharp
public enum CustomerErrorKind { NotFound, EmailAlreadyExists, AlreadyInactive, RepositoryUnavailable }

public record CustomerError(string Message, CustomerErrorKind Kind) : BaseError(Message)
{
    public override int StatusCode => Kind switch
    {
        CustomerErrorKind.NotFound            => StatusCodes.Status404NotFound,
        CustomerErrorKind.EmailAlreadyExists  => StatusCodes.Status409Conflict,
        CustomerErrorKind.AlreadyInactive     => StatusCodes.Status409Conflict,
        _                                     => StatusCodes.Status500InternalServerError
    };

    public static CustomerError NotFound(Guid id)
        => new($"Customer {id} not found", CustomerErrorKind.NotFound)
           { ErrorCode = "CUSTOMER_NOT_FOUND" };

    public static CustomerError EmailAlreadyExists(string email)
        => new($"Email {email} is already registered", CustomerErrorKind.EmailAlreadyExists)
           { ErrorCode = "CUSTOMER_EMAIL_EXISTS" };

    public static CustomerError AlreadyInactive(Guid id)
        => new($"Customer {id} is already inactive", CustomerErrorKind.AlreadyInactive)
           { ErrorCode = "CUSTOMER_ALREADY_INACTIVE" };

    public static CustomerError RepositoryUnavailable(Exception ex)
        => new("Customer data store unavailable", CustomerErrorKind.RepositoryUnavailable)
           { InnerException = ex };
}
```

### CustomerService — clean .Bind() chains, NO mid-chain .IsSuccess checks
```csharp
public Task<Result<CustomerError, CustomerDto>> CreateCustomerAsync(
    CreateCustomerRequest request, CancellationToken ct)
    => CheckEmailNotTakenAsync(request.Email, ct)
       .BindAsync(email => _repo.InsertAsync(Customer.Create(request.Name, email), ct))
       .MapAsync(ToDto);

public Task<Result<CustomerError, CustomerDto>> DeactivateCustomerAsync(Guid id, CancellationToken ct)
    => _repo.GetByIdAsync(id, ct)
       .BindAsync(ValidateActiveStatus)
       .BindAsync(customer => _repo.UpdateAsync(customer.Deactivate(), ct))
       .MapAsync(ToDto);
```

### ICustomerLookup — inter-BC contract (in Customers.Contracts, not Customers)
```csharp
// Exemplar.Customers.Contracts/ICustomerLookup.cs
public interface ICustomerLookup
{
    Task<Result<NotFoundError, CustomerSummaryDto>> FindByIdAsync(Guid id, CancellationToken ct);
}
// CustomerService implements ICustomerService AND ICustomerLookup
```

### Repository — returns Result, no raw exceptions
```csharp
public async Task<Result<CustomerError, Customer>> GetByIdAsync(Guid id, CancellationToken ct)
{
    try
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        var customer = await conn.QuerySingleOrDefaultAsync<Customer>(...);
        return customer is null
            ? CustomerError.NotFound(id)
            : Result<CustomerError, Customer>.Success(customer);
    }
    catch (Exception ex)
    {
        return CustomerError.RepositoryUnavailable(ex);
    }
}
```

## Quality Gates

- [x] `dotnet build` — zero warnings, zero errors
- [x] `dotnet test` unit tests — 11/11 passed, >80% line coverage
- [x] Zero mid-chain `.IsSuccess` or `.IsFailure` checks in `CustomerService`
- [x] All 4 endpoints return correct HTTP status codes for all error kinds
- [x] `CreateCustomer` returns 201 with `Location` header on success
- [x] `DeactivateCustomer` returns 404 for unknown customer, 409 if already inactive

## Implementation Notes

- FastEndpoints 8.x uses `Send.OkAsync()` / `Send.ResponseAsync()` API — `SendAsync()` from earlier versions is replaced
- Authorization: `Roles("Admin")` for admin endpoints; `AllowAnonymous()` absent means default policy applies (wired in TASK-W4)
- `AlreadyInactive` added to `CustomerErrorKind` beyond the original spec to satisfy the 409 deactivation quality gate
- `CustomerApiFixture` uses `FakeAuthHandler` (all requests as Admin) so integration tests run without JWT infrastructure
- `DefaultTypeMap.MatchNamesWithUnderscores = true` set in `CustomerRepository` static constructor for snake_case→PascalCase Dapper mapping

## Next Wave

TASK-W3 (Addresses BC) depends on `ICustomerLookup` from `Customers.Contracts/`.
