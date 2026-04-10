---
id: TASK-W2
title: "Customers BC — domain, repository, service, 4 endpoints, unit + integration tests"
status: backlog
task_type: feature
wave: 2
created: 2026-04-10T00:00:00Z
updated: 2026-04-10T00:00:00Z
priority: high
complexity: 6
parent_review: TASK-59B3
feature_id: dotnet-fastendpoints-exemplar
dependencies: [TASK-W1]
tags: [dotnet, fastendpoints, customers, bounded-context]
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
    Validators/
      CreateCustomerValidator.cs
  Infrastructure/
    ICustomerRepository.cs
    CustomerRepository.cs

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
```

## Key Implementation Rules

### CustomerError — Pattern A (enum-discriminated + static factories)
```csharp
public enum CustomerErrorKind { NotFound, EmailAlreadyExists, RepositoryUnavailable }

public record CustomerError(string Message, CustomerErrorKind Kind) : BaseError(Message)
{
    public override int StatusCode => Kind switch
    {
        CustomerErrorKind.NotFound           => StatusCodes.Status404NotFound,
        CustomerErrorKind.EmailAlreadyExists => StatusCodes.Status409Conflict,
        _                                    => StatusCodes.Status500InternalServerError
    };

    public static CustomerError NotFound(Guid id)
        => new($"Customer {id} not found", CustomerErrorKind.NotFound)
           { ErrorCode = "CUSTOMER_NOT_FOUND" };

    public static CustomerError EmailAlreadyExists(string email)
        => new($"Email {email} is already registered", CustomerErrorKind.EmailAlreadyExists)
           { ErrorCode = "CUSTOMER_EMAIL_EXISTS" };

    public static CustomerError RepositoryUnavailable(Exception ex)
        => new("Customer data store unavailable", CustomerErrorKind.RepositoryUnavailable)
           { InnerException = ex };
}
```

### CustomerService — clean .Bind() chains, NO mid-chain .IsSuccess checks
```csharp
public async Task<Result<CustomerError, CustomerDto>> CreateCustomerAsync(
    CreateCustomerRequest request, CancellationToken ct)
{
    return await ValidateEmailFormat(request.Email)
        .BindAsync(email => CheckEmailNotTakenAsync(email, ct))
        .BindAsync(email => _repo.InsertAsync(Customer.Create(request.Name, email), ct))
        .Map(customer => customer.ToDto());
}
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
        var customer = await _db.QuerySingleOrDefaultAsync<Customer>(...);
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

- [ ] `dotnet build` — zero warnings, zero errors
- [ ] `dotnet test Exemplar.Customers.Tests` — 100% pass, ≥80% line coverage
- [ ] Zero mid-chain `.IsSuccess` or `.IsFailure` checks in `CustomerService`
- [ ] All 4 endpoints return correct HTTP status codes for all error kinds
- [ ] `CreateCustomer` returns 201 with `Location` header on success
- [ ] `DeactivateCustomer` returns 404 for unknown customer, 409 if already inactive

## Next Wave

TASK-W3 (Addresses BC) depends on `ICustomerLookup` from `Customers.Contracts/`.
