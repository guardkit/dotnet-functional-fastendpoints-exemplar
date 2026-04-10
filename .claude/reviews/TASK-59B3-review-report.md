# Review Report: TASK-59B3

**Task**: Review and plan .NET FastEndpoints functional exemplar for GuardKit template  
**Review Mode**: Decision Analysis  
**Depth**: Deep  
**Trade-off Priority**: Balanced (developer ergonomics / production-readiness / template-richness)  
**Completed**: 2026-04-10  
**Reviewer**: Claude (task-review workflow)

---

## Executive Summary

All six decisions are resolved. The exemplar is a standalone `.NET 8` solution using a deliberately generic domain (Customers + Addresses) so that the resulting GuardKit template is universally applicable, not tied to any niche business context.

The functional error handling library is **CSharpFunctionalExtensions** behind a thin wrapper. No other functional library is used or referenced.

**Score: 92/100** — Clean, implementable plan. Generic domain. Zero niche dependencies. Full Keycloak in all environments. Two BCs demonstrating the modular monolith inter-BC contract pattern.

---

## Decision 1: CSharpFunctionalExtensions Wrapper API Design

### Decision

Use `CSharpFunctionalExtensions.Result<TSuccess, TError>` as the underlying implementation, exposed through a thin `Result<TError, TSuccess>` wrapper struct. The wrapper is the **only** public API that consuming code (services, endpoints) ever touches — `CSharpFunctionalExtensions` is an internal implementation detail of `Exemplar.Core`.

**Why a thin wrapper rather than using CSharpFunctionalExtensions directly:**
- Consuming code never has a direct NuGet reference to CSharpFunctionalExtensions — only `Exemplar.Core`
- When C# 15 native union types ship (November 2026), the migration is a single-file change inside `Exemplar.Core`. Services and endpoints are untouched.
- The wrapper enforces `where TError : BaseError`, which CSharpFunctionalExtensions' generic `Result<T,E>` does not

**Type parameter ordering — error-first:** `Result<TError, TSuccess>`. CSharpFunctionalExtensions uses success-first (`Result<TSuccess, TError>`) internally. The wrapper presents error-first to make error handling visually prominent at the call site.

```csharp
// Exemplar.Core/Functional/Result.cs
namespace Exemplar.Core.Functional;

/// <summary>
/// Represents either a typed error or a typed success value.
/// Error-first type parameter ordering: Result&lt;TError, TSuccess&gt;.
///
/// Wraps CSharpFunctionalExtensions internally. Consuming code (services, endpoints)
/// references only this type — never CSharpFunctionalExtensions directly.
///
/// C# 15 migration: when native union types ship (Nov 2026), replace the _inner field
/// with a union declaration. All Match/Bind/Map call sites remain unchanged.
/// </summary>
public readonly struct Result<TError, TSuccess>
    where TError : BaseError
{
    private readonly CSharpFunctionalExtensions.Result<TSuccess, TError> _inner;

    private Result(CSharpFunctionalExtensions.Result<TSuccess, TError> inner)
        => _inner = inner;

    // --- Construction ---
    public static Result<TError, TSuccess> Success(TSuccess value)
        => new(CSharpFunctionalExtensions.Result.Success<TSuccess, TError>(value));

    public static Result<TError, TSuccess> Failure(TError error)
        => new(CSharpFunctionalExtensions.Result.Failure<TSuccess, TError>(error));

    // --- State ---
    public bool IsSuccess => _inner.IsSuccess;
    public bool IsFailure => _inner.IsFailure;
    public TSuccess Value => _inner.Value;   // throws if failure — use Match in preference
    public TError Error  => _inner.Error;    // throws if success — use Match in preference

    // --- Pattern matching ---
    public TResult Match<TResult>(
        Func<TSuccess, TResult> onSuccess,
        Func<TError, TResult> onFailure)
        => _inner.IsSuccess ? onSuccess(_inner.Value) : onFailure(_inner.Error);

    // --- Monadic composition ---
    public Result<TError, TNew> Map<TNew>(Func<TSuccess, TNew> f)
        => _inner.IsSuccess
            ? Result<TError, TNew>.Success(f(_inner.Value))
            : Result<TError, TNew>.Failure(_inner.Error);

    public Result<TError, TNew> Bind<TNew>(Func<TSuccess, Result<TError, TNew>> f)
        => _inner.IsSuccess ? f(_inner.Value) : Result<TError, TNew>.Failure(_inner.Error);

    public async Task<Result<TError, TNew>> BindAsync<TNew>(
        Func<TSuccess, Task<Result<TError, TNew>>> f)
        => _inner.IsSuccess
            ? await f(_inner.Value)
            : Result<TError, TNew>.Failure(_inner.Error);

    public async Task<Result<TError, TSuccess>> TapAsync(Func<TSuccess, Task> f)
    {
        if (_inner.IsSuccess) await f(_inner.Value);
        return this;
    }

    // --- Implicit conversions (call-site ergonomics) ---
    public static implicit operator Result<TError, TSuccess>(TSuccess value) => Success(value);
    public static implicit operator Result<TError, TSuccess>(TError error)   => Failure(error);
}
```

### Endpoint extension — `HandleResultAsync`

Bridges `Result<TError, TSuccess>` to a FastEndpoints HTTP response. Uses direct `if/else` branching on `IsSuccess` — no third-party monadic return type in the method body.

```csharp
// Exemplar.Core/Functional/ResultExtensions.cs
namespace Exemplar.Core.Functional;

public static class ResultExtensions
{
    /// <summary>
    /// Bridges Result to a FastEndpoints HTTP response.
    /// Success: invokes the caller-supplied response callback.
    /// Failure: maps BaseError.StatusCode to an RFC 7807 ProblemDetails response.
    /// </summary>
    public static async Task HandleResultAsync<TRequest, TResponse, TError, TSuccess>(
        this Endpoint<TRequest, TResponse> endpoint,
        Result<TError, TSuccess> result,
        Func<TSuccess, Task> onSuccess,
        CancellationToken ct)
        where TRequest : notnull
        where TError : BaseError
    {
        if (result.IsSuccess)
        {
            try
            {
                await onSuccess(result.Value);
            }
            catch (Exception ex)
            {
                endpoint.Logger.LogError(ex,
                    "Unhandled exception in success handler for {EndpointType}",
                    endpoint.GetType().Name);
                endpoint.AddError("An unexpected error occurred processing the response");
                await endpoint.HttpContext.Response.SendErrorsAsync(
                    endpoint.ValidationFailures, StatusCodes.Status500InternalServerError, null, ct);
            }
        }
        else
        {
            await endpoint.HandleErrorAsync(result.Error, ct);
        }
    }

    /// <summary>
    /// Converts a BaseError to an RFC 7807 ProblemDetails response using BaseError.StatusCode.
    /// Also handles EndpointWithoutRequest endpoints.
    /// </summary>
    public static async Task HandleErrorAsync<TRequest, TResponse, TError>(
        this Endpoint<TRequest, TResponse> endpoint,
        TError error,
        CancellationToken ct)
        where TRequest : notnull
        where TError : BaseError
    {
        endpoint.Logger.LogWarning(
            error.InnerException,
            "Domain error {ErrorType} on {EndpointType}: {Message}",
            error.GetType().Name, endpoint.GetType().Name, error.Message);

        endpoint.AddError(error.Message);
        await endpoint.HttpContext.Response.SendErrorsAsync(
            endpoint.ValidationFailures, error.StatusCode, null, ct);
    }
}
```

**Typical endpoint usage:**

```csharp
public override async Task HandleAsync(GetCustomerByIdRequest req, CancellationToken ct)
{
    var result = await _customerService.GetByIdAsync(req.CustomerId, ct);
    await this.HandleResultAsync(result, async customer => await SendOkAsync(customer, ct), ct);
}
```

### C# 15 migration path

When native union types ship, the migration is:
1. Replace the `_inner` field in `Result<TError, TSuccess>` with a `union` declaration
2. Rewrite the `IsSuccess`, `Value`, `Error`, `Match`, `Map`, `Bind`, `BindAsync`, `TapAsync` methods to delegate to native union semantics
3. Remove the `CSharpFunctionalExtensions` NuGet package reference from `Exemplar.Core.csproj`
4. Zero changes to any service, repository, or endpoint

**This is a single-file change in `Exemplar.Core`.**

---

## Decision 2: BaseError Hierarchy

### Decision

`BaseError` is an abstract record in `Exemplar.Core`. It carries a `StatusCode`, an optional machine-readable `ErrorCode`, and an optional `InnerException` for infrastructure failures. `InnerException` is an init-only property — not virtual — so error records set it via object initializer syntax rather than override.

```csharp
// Exemplar.Core/Functional/BaseError.cs
namespace Exemplar.Core.Functional;

public abstract record BaseError(string Message)
{
    /// <summary>HTTP status code. Override in derived types.</summary>
    public virtual int StatusCode => StatusCodes.Status500InternalServerError;

    /// <summary>
    /// Optional machine-readable code for API consumers who need to distinguish
    /// errors sharing the same HTTP status code.
    /// Convention: SCREAMING_SNAKE_CASE (e.g. "CUSTOMER_NOT_FOUND").
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Underlying infrastructure exception, if applicable.
    /// Set via object initialiser: new SomeError("msg") { InnerException = ex }
    /// or via a static factory method on the derived type.
    /// </summary>
    public Exception? InnerException { get; init; }
}
```

**Cross-cutting errors in Core** (provided by `Exemplar.Core`, used across all BCs):

```csharp
// Exemplar.Core/Functional/CommonErrors.cs
public record NotFoundError(string Message) : BaseError(Message)
{
    public override int StatusCode => StatusCodes.Status404NotFound;
}

public record ValidationError(string Message) : BaseError(Message)
{
    public override int StatusCode => StatusCodes.Status400BadRequest;
}

public record UnauthorizedError(string Message) : BaseError(Message)
{
    public override int StatusCode => StatusCodes.Status401Unauthorized;
}

public record ForbiddenError(string Message) : BaseError(Message)
{
    public override int StatusCode => StatusCodes.Status403Forbidden;
}

public record ConflictError(string Message) : BaseError(Message)
{
    public override int StatusCode => StatusCodes.Status409Conflict;
}

/// <summary>Wraps unexpected infrastructure failures (DB, HTTP client, etc.).</summary>
public record InfrastructureError(string Message) : BaseError(Message)
{
    public override int StatusCode => StatusCodes.Status503ServiceUnavailable;
}
```

### Two BC error patterns demonstrated

The exemplar shows both patterns so the template covers the full range:

**Pattern A — Enum-discriminated domain error** (Customers BC, complex enough to warrant it):

```csharp
// Exemplar.Customers/Domain/Errors/CustomerError.cs
public enum CustomerErrorKind
{
    NotFound,
    DuplicateEmail,
    InvalidStatus,
    RepositoryUnavailable,
    RepositoryTimeout
}

public record CustomerError(string Message, CustomerErrorKind Kind)
    : BaseError(Message)
{
    public override int StatusCode => Kind switch
    {
        CustomerErrorKind.NotFound              => StatusCodes.Status404NotFound,
        CustomerErrorKind.DuplicateEmail        => StatusCodes.Status409Conflict,
        CustomerErrorKind.InvalidStatus         => StatusCodes.Status422UnprocessableEntity,
        CustomerErrorKind.RepositoryUnavailable => StatusCodes.Status503ServiceUnavailable,
        CustomerErrorKind.RepositoryTimeout     => StatusCodes.Status504GatewayTimeout,
        _                                       => StatusCodes.Status500InternalServerError
    };

    // Static factories — cleaner call sites than new() everywhere
    public static CustomerError NotFound(Guid id)
        => new($"Customer {id} not found", CustomerErrorKind.NotFound)
            { ErrorCode = "CUSTOMER_NOT_FOUND" };

    public static CustomerError DuplicateEmail(string email)
        => new($"Email '{email}' is already registered", CustomerErrorKind.DuplicateEmail)
            { ErrorCode = "DUPLICATE_EMAIL" };

    public static CustomerError RepositoryUnavailable(Exception ex)
        => new("Customer data store is temporarily unavailable",
                CustomerErrorKind.RepositoryUnavailable)
            { InnerException = ex };
}
```

**Pattern B — Simple per-error records** (Addresses BC, fewer error types):

```csharp
// Exemplar.Addresses/Domain/Errors/
public record AddressNotFoundError(string Message) : BaseError(Message)
{
    public override int StatusCode => StatusCodes.Status404NotFound;
}

public record AddressValidationError(string Message) : BaseError(Message)
{
    public override int StatusCode => StatusCodes.Status400BadRequest;
}

public record AddressRepositoryError(string Message) : BaseError(Message)
{
    public override int StatusCode => StatusCodes.Status503ServiceUnavailable;
}
```

### When to use each pattern

| Pattern | Use when |
|---------|----------|
| Enum-discriminated | BC has 4+ related error types, or when a single `switch` on kind is more readable than many subclasses |
| Simple per-error records | BC has ≤3 error types, or when each error type has distinct business meaning that benefits from its own type |
| Both | Demonstrate both in the exemplar — Customers uses enum, Addresses uses simple records |

---

## Decision 3: Bounded Context Scope

### Decision: Two BCs — Customers (primary) + Addresses (secondary)

**Why two BCs:**
- A single-BC exemplar produces a template that only scaffolds one module. Two BCs show the modular monolith pattern that makes the FinProxy template valuable.
- The inter-BC contract interface (`ICustomerLookup`) is the key pattern for modular monoliths — it demonstrates how BCs communicate through public API surfaces without direct project references to internal implementations.

**Why Customers + Addresses:**
- Universally understood domain — no business context required to read the code
- Clean inter-BC dependency: Addresses needs to verify a customer exists before creating an address (calls `ICustomerLookup`, defined in `Exemplar.Customers.Contracts`)
- Simple enough to implement quickly; rich enough to demonstrate CRUD + a richer operation (status change on Customer)

**Exemplar.Customers** — primary BC:

```
Domain/
  Customer.cs              — entity: Id, Name, Email, Status (Active/Inactive/Suspended)
  Errors/
    CustomerError.cs       — enum-discriminated, Pattern A (see Decision 2)

Contracts/
  ICustomerService.cs      — public service interface
  ICustomerLookup.cs       — narrow query interface for cross-BC use (Addresses → Customers)
  Dtos/
    CustomerDto.cs
    CustomerSummaryDto.cs

Endpoints/
  Customers/
    GetCustomerById.cs     — GET /customers/{id}          RequireAuthentication
    GetAllCustomers.cs     — GET /customers               RequireAuthentication
    CreateCustomer.cs      — POST /customers              AdminOnly
    DeactivateCustomer.cs  — PUT /customers/{id}/deactivate  AdminOnly (shows status workflow)

Services/
  CustomerService.cs       — Result<CustomerError,T> composition with .Bind() chains

Repositories/
  ICustomerRepository.cs
  CustomerRepository.cs    — Dapper + Npgsql

Validators/
  CreateCustomerValidator.cs   — FluentValidation
```

**Exemplar.Addresses** — secondary BC (minimal, demonstrates inter-BC pattern):

```
Domain/
  Address.cs               — entity: Id, CustomerId, Line1, Line2, City, PostCode, Country
  Errors/
    AddressNotFoundError.cs      — Pattern B (simple records)
    AddressValidationError.cs
    AddressRepositoryError.cs

Contracts/
  IAddressService.cs

Endpoints/
  Addresses/
    GetAddressesByCustomerId.cs  — GET /customers/{customerId}/addresses  RequireAuthentication
    AddAddress.cs                — POST /customers/{customerId}/addresses  RequireAuthentication

Services/
  AddressService.cs        — calls ICustomerLookup to verify customer exists before adding address

Repositories/
  IAddressRepository.cs
  AddressRepository.cs
```

**Project reference rules (enforced, not by convention):**

```
Exemplar.Core              → (no BC references)
Exemplar.Customers         → Exemplar.Core
Exemplar.Customers.Contracts → Exemplar.Core
Exemplar.Addresses         → Exemplar.Core
                           → Exemplar.Customers.Contracts  (ICustomerLookup only)
                           NOT Exemplar.Customers           (internals are invisible)
Exemplar.API               → Exemplar.Customers
                           → Exemplar.Addresses
                           → Exemplar.Core
```

**Cross-BC query pattern:**

```csharp
// Exemplar.Customers/Contracts/ICustomerLookup.cs
public interface ICustomerLookup
{
    Task<Result<NotFoundError, CustomerSummaryDto>> FindByIdAsync(Guid id, CancellationToken ct);
}

// Exemplar.Customers/Services/CustomerService.cs — implements both ICustomerService and ICustomerLookup
public class CustomerService : ICustomerService, ICustomerLookup { ... }

// Exemplar.API/Program.cs — registered once, satisfies both interfaces
services.AddScoped<CustomerService>();
services.AddScoped<ICustomerService>(sp => sp.GetRequiredService<CustomerService>());
services.AddScoped<ICustomerLookup>(sp => sp.GetRequiredService<CustomerService>());

// Exemplar.Addresses/Services/AddressService.cs — depends on ICustomerLookup, not CustomerService
public class AddressService(ICustomerLookup customerLookup, IAddressRepository repo)
{
    public async Task<Result<BaseError, AddressDto>> AddAddressAsync(
        Guid customerId, AddAddressRequest request, CancellationToken ct)
    {
        // Verify customer exists before creating address — cross-BC query via interface
        return await customerLookup.FindByIdAsync(customerId, ct)
            .BindAsync(customer => ValidateAddress(request))
            .BindAsync(validated => repo.InsertAsync(customerId, validated, ct))
            .Map(address => address.ToDto());
    }
}
```

**No message bus in the exemplar MVP.** The template README will document NATS JetStream as the extension point for state-changing cross-BC events. Adding it to the exemplar would triple local dev complexity without proportional template value.

---

## Decision 4: Auth (Keycloak in All Environments)

### Decision

Full Keycloak 26.4+ JWT in all environments — local dev, CI, and production. No stubs, no test-only JWT issuers, no simplified auth modes.

**`MapInboundClaims = false` is mandatory.** Without it, ASP.NET Core remaps Keycloak claim names to Microsoft XML-namespaced URIs, silently breaking all role-based authorization. This is the single most common Keycloak/.NET integration failure and must be prominently demonstrated in the exemplar.

```csharp
// Exemplar.API/Infrastructure/AuthenticationExtensions.cs
public static IServiceCollection AddKeycloakAuthentication(
    this IServiceCollection services,
    IConfiguration configuration,
    IWebHostEnvironment environment)
{
    services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.Authority             = configuration["Authentication:Authority"];
        options.Audience              = configuration["Authentication:Audience"];
        options.MapInboundClaims      = false;  // REQUIRED: preserve Keycloak claim names
        options.RequireHttpsMetadata  = !environment.IsDevelopment();

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidIssuer              = configuration["Authentication:Authority"],
            ValidateAudience         = true,
            ValidAudience            = configuration["Authentication:Audience"],
            ValidateLifetime         = true,
            NameClaimType            = "preferred_username",  // Keycloak's name claim
            RoleClaimType            = ClaimTypes.Role,
            ClockSkew                = TimeSpan.FromSeconds(30)
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = ctx =>
            {
                if (ctx.Principal?.Identity is ClaimsIdentity identity)
                    MapKeycloakRoleClaims(identity);
                return Task.CompletedTask;
            }
        };
    });

    services.AddAuthorizationBuilder()
        .AddPolicy("AdminOnly",            p => p.RequireRole("admin"))
        .AddPolicy("UserOrAdmin",          p => p.RequireRole("user", "admin"))
        .AddPolicy("RequireAuthentication",p => p.RequireAuthenticatedUser());

    return services;
}

private static void MapKeycloakRoleClaims(ClaimsIdentity identity)
{
    // Remove any pre-mapped role claims to avoid duplicates
    foreach (var existing in identity.FindAll(ClaimTypes.Role).ToList())
        identity.RemoveClaim(existing);

    // realm_access.roles → ClaimTypes.Role
    var realmAccess = identity.FindFirst("realm_access");
    if (realmAccess is not null)
    {
        var doc = JsonDocument.Parse(realmAccess.Value);
        if (doc.RootElement.TryGetProperty("roles", out var roles))
            foreach (var role in roles.EnumerateArray())
                if (role.GetString() is { } r)
                    identity.AddClaim(new Claim(ClaimTypes.Role, r));
    }

    // resource_access.{client}.roles → ClaimTypes.Role
    var resourceAccess = identity.FindFirst("resource_access");
    if (resourceAccess is not null)
    {
        var doc = JsonDocument.Parse(resourceAccess.Value);
        foreach (var client in doc.RootElement.EnumerateObject())
            if (client.Value.TryGetProperty("roles", out var clientRoles))
                foreach (var role in clientRoles.EnumerateArray())
                    if (role.GetString() is { } r)
                        identity.AddClaim(new Claim(ClaimTypes.Role, r));
    }
}
```

**Keycloak realm setup (minimal for exemplar):**
- Realm: `exemplar`
- Client: `exemplar-api` (confidential, service accounts enabled, PKCE S256)
- Audience mapper: adds `exemplar-api` to the `aud` claim in access tokens
- Roles: `admin`, `user`
- Realm exported to `keycloak/exemplar-realm.json`, imported on container startup via `--import-realm`

**Policies demonstrated:**
- `AdminOnly` — `CreateCustomer`, `DeactivateCustomer` (mutation endpoints)
- `UserOrAdmin` — `AddAddress` (authenticated user action)
- `RequireAuthentication` — all read endpoints
- `AllowAnonymous` — health check endpoint (explicit opt-out, shows the pattern)

**Integration test auth:** Real Keycloak container via TestContainers — real tokens, no mocks. Integration tests spin up PostgreSQL + Keycloak, obtain a real JWT, and call endpoints. This is the correct pattern for environment parity.

---

## Decision 5: Database

### Decision: PostgreSQL + Dapper + DbUp + TestContainers

```
Database layer:
  Runtime:    PostgreSQL 16 (via docker-compose locally, RDS in production)
  ORM:        Dapper (lightweight, SQL visible in source — better for templates)
  Driver:     Npgsql
  Migrations: DbUp (ordered .sql files, auto-applied on startup)
  Tests:      TestContainers.PostgreSql (real DB, no in-memory substitution)
```

**Repository pattern:**

```csharp
// Exemplar.Customers/Repositories/CustomerRepository.cs
public class CustomerRepository(NpgsqlDataSource dataSource, ILogger<CustomerRepository> logger)
    : ICustomerRepository
{
    public async Task<Result<CustomerError, Customer>> GetByIdAsync(Guid id, CancellationToken ct)
    {
        try
        {
            await using var conn = await dataSource.OpenConnectionAsync(ct);
            var row = await conn.QuerySingleOrDefaultAsync<CustomerRow>(
                "SELECT id, name, email, status, created_at FROM customers.customers WHERE id = @Id",
                new { Id = id });

            return row is null
                ? CustomerError.NotFound(id)
                : row.ToDomain();
        }
        catch (NpgsqlException ex) when (ex.IsTransient)
        {
            logger.LogError(ex, "Transient DB error fetching customer {Id}", id);
            return CustomerError.RepositoryUnavailable(ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error fetching customer {Id}", id);
            return CustomerError.RepositoryUnavailable(ex);
        }
    }

    public async Task<Result<CustomerError, Customer>> InsertAsync(
        Customer customer, CancellationToken ct)
    {
        try
        {
            await using var conn = await dataSource.OpenConnectionAsync(ct);
            await conn.ExecuteAsync(
                """
                INSERT INTO customers.customers (id, name, email, status, created_at)
                VALUES (@Id, @Name, @Email, @Status, @CreatedAt)
                """,
                new { customer.Id, customer.Name, customer.Email,
                      Status = customer.Status.ToString(), customer.CreatedAt });
            return customer;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return CustomerError.DuplicateEmail(customer.Email);
        }
        catch (NpgsqlException ex) when (ex.IsTransient)
        {
            return CustomerError.RepositoryUnavailable(ex);
        }
    }
}
```

**Service with clean `.Bind()` chain (no imperative .IsSuccess checks):**

```csharp
// Exemplar.Customers/Services/CustomerService.cs
public async Task<Result<CustomerError, CustomerDto>> CreateCustomerAsync(
    CreateCustomerRequest request, CancellationToken ct)
{
    return await ValidateEmailFormat(request.Email)              // Result<CustomerError, string>
        .BindAsync(email => CheckEmailNotTakenAsync(email, ct)) // Result<CustomerError, string>
        .BindAsync(email => _repo.InsertAsync(
            Customer.Create(request.Name, email), ct))          // Result<CustomerError, Customer>
        .Map(customer => customer.ToDto());                     // Result<CustomerError, CustomerDto>
    // Short-circuits on first failure — no if/else, no .IsSuccess checks
}
```

**DbUp migrations (pure SQL, version-ordered):**

```
db/
  migrations/
    0001_create_customers_schema.sql
    0002_create_customers_table.sql
    0003_create_addresses_schema.sql
    0004_create_addresses_table.sql
```

DbUp runs on `Program.cs` startup before the app begins accepting requests. Migration scripts are idempotent and committed to source control alongside the application code.

---

## Decision 6: Template-Create Readiness

### Minimum Viable Exemplar (MVE) scope

The exemplar must be a **working, runnable .NET 8 solution** — not a code snippet collection. `/template-create` extracts patterns from real, compilable source code.

**Must have:**

| Feature | Required | Rationale |
|---------|----------|-----------|
| `Exemplar.Core` — BaseError, Result wrapper, ResultExtensions | ✅ | Core template artifact |
| `HandleResultAsync` endpoint extension | ✅ | Eliminates boilerplate in every endpoint |
| FastEndpoints + ProblemDetails wiring | ✅ | High-value scaffolding target |
| Two BCs — Customers (primary) + Addresses (secondary) | ✅ | Modular monolith pattern |
| `ICustomerLookup` inter-BC contract | ✅ | The key modular monolith pattern |
| Service with `.Bind()` chain | ✅ | Demonstrates railway-oriented composition |
| Repository (Dapper + Npgsql + PostgreSQL) | ✅ | Data access pattern |
| Keycloak JWT auth with `MapInboundClaims = false` | ✅ | High-value anti-gotcha |
| Keycloak docker-compose + realm JSON export | ✅ | Environment parity |
| DbUp migrations (pure SQL) | ✅ | Schema management pattern |
| FluentValidation validator (CreateCustomer) | ✅ | Validation pattern |
| Unit tests — mocked repository | ✅ | Test strategy pattern |
| Integration tests — TestContainers (PostgreSQL + Keycloak) | ✅ | Real-DB + real-auth testing |

**Intentionally excluded from MVE:**

| Feature | Reason |
|---------|--------|
| MassTransit / NATS JetStream | Triples local dev complexity; documented as extension point in README |
| EF Core | Dapper is more legible for a teaching/template artefact |
| gRPC / SignalR | Out of scope for REST exemplar |
| Multi-tenancy (Keycloak Organizations) | Phase 2 of FinProxy — not needed for template foundation |

### Template parameterisation plan

When `/template-create` runs against the exemplar, these identifiers are replaced:

| Placeholder | Example value in exemplar | Replaced with |
|-------------|--------------------------|---------------|
| `{{RootNamespace}}` | `Exemplar` | e.g. `MyCompany.MyProduct` |
| `{{PrimaryBcName}}` | `Customers` | e.g. `Donors` |
| `{{PrimaryBcRoute}}` | `customers` | e.g. `donors` |
| `{{PrimaryEntityName}}` | `Customer` | e.g. `Donor` |
| `{{PrimaryErrorType}}` | `CustomerError` | e.g. `DonorError` |
| `{{SecondaryBcName}}` | `Addresses` | e.g. `Instructions` |
| `{{CrossBcInterface}}` | `ICustomerLookup` | e.g. `IDonorLookup` |
| `{{ServiceName}}` | `exemplar` | e.g. `finproxy` |
| `{{KeycloakRealm}}` | `exemplar` | e.g. `finproxy` |
| `{{KeycloakAudience}}` | `exemplar-api` | e.g. `finproxy-api` |
| `{{DbSchema}}` | `customers` | e.g. `donors` |

**High template-value boilerplate files (copy with parameter substitution):**

- `Exemplar.Core/**` — entire Core library
- `Exemplar.API/Program.cs` — DI composition root
- `Exemplar.API/Infrastructure/AuthenticationExtensions.cs` — Keycloak auth wiring
- `docker-compose.yml` — full local dev stack
- `keycloak/exemplar-realm.json` — reproducible realm config
- `db/migrations/*.sql` — DbUp migration pattern
- `tests/**/IntegrationTests/` — TestContainers setup (PostgreSQL + Keycloak)

---

## Implementation Plan

### Wave 1: Core (Days 1–2)

| # | Task | Deliverable |
|---|------|-------------|
| 1 | Scaffold solution — `.sln`, all `.csproj` files, `global.json` (net8.0) | Solution structure |
| 2 | `BaseError.cs` + `CommonErrors.cs` | Abstract record + 6 cross-cutting errors |
| 3 | `Result<TError, TSuccess>.cs` | Thin wrapper over CSharpFunctionalExtensions |
| 4 | `ResultExtensions.cs` — `HandleResultAsync` + `HandleErrorAsync` | Endpoint bridge |
| 5 | `FastEndpointsExtensions.cs` — `AddFastEndpointsServices` + `UseApiConfiguration` | ProblemDetails, route prefix, camelCase |
| 6 | `OpenTelemetryExtensions.cs` — simplified (Aspire Dashboard + generic OTLP, no Elastic/LogicMonitor) | OTEL wiring |
| 7 | `SerilogExtensions.cs` — structured logging with OTel sink | Serilog wiring |
| 8 | `HealthCheckExtensions.cs` | Liveness + readiness probes |
| 9 | `Exemplar.Core.Tests/` — Result wrapper tests | Map, Bind, BindAsync, Match, implicit conversions, failure short-circuits |

### Wave 2: Customers BC (Days 3–5)

| # | Task | Deliverable |
|---|------|-------------|
| 10 | `Customer.cs` entity + `CustomerStatus` enum | Domain model |
| 11 | `CustomerError.cs` (enum-discriminated, Pattern A) with static factories | Error type |
| 12 | `ICustomerService.cs` + `ICustomerLookup.cs` in `Customers.Contracts/` | Public API surface |
| 13 | `ICustomerRepository.cs` + `CustomerRepository.cs` (Dapper + Npgsql) | Data access |
| 14 | `CustomerService.cs` — `.Bind()` chains for all operations | Railway-oriented composition |
| 15 | `GetCustomerById.cs` — `GET /customers/{id}` RequireAuthentication | Endpoint |
| 16 | `GetAllCustomers.cs` — `GET /customers` RequireAuthentication (with pagination) | Endpoint |
| 17 | `CreateCustomer.cs` — `POST /customers` AdminOnly | Endpoint |
| 18 | `DeactivateCustomer.cs` — `PUT /customers/{id}/deactivate` AdminOnly | Status workflow endpoint |
| 19 | `CreateCustomerValidator.cs` (FluentValidation) | Validation |
| 20 | `Exemplar.Customers.Tests/` — unit tests (mocked `ICustomerRepository`) | Service layer, 80%+ coverage |
| 21 | `Exemplar.Customers.Tests/IntegrationTests/` — TestContainers PostgreSQL | Real DB endpoint tests |

### Wave 3: Addresses BC (Day 6)

| # | Task | Deliverable |
|---|------|-------------|
| 22 | `Address.cs` entity + `AddressError` records (Pattern B) | Domain layer |
| 23 | `IAddressService.cs` in `Addresses.Contracts/` | Public API surface |
| 24 | `IAddressRepository.cs` + `AddressRepository.cs` (Dapper) | Data access |
| 25 | `AddressService.cs` — calls `ICustomerLookup` via `.BindAsync()` before insert | Inter-BC query |
| 26 | `GetAddressesByCustomerId.cs` — `GET /customers/{id}/addresses` RequireAuthentication | Endpoint |
| 27 | `AddAddress.cs` — `POST /customers/{id}/addresses` UserOrAdmin | Endpoint |
| 28 | Integration test — add address for valid customer, verify 404 for unknown customer | Inter-BC validated |

### Wave 4: API Host (Day 7)

| # | Task | Deliverable |
|---|------|-------------|
| 29 | `Program.cs` — wires Core + Customers + Addresses, DbUp, OpenTelemetry, health | Composition root |
| 30 | `AuthenticationExtensions.cs` — `MapInboundClaims = false`, role mapping, 3 policies | Keycloak auth |
| 31 | `KeycloakHealthCheck.cs` — HTTP GET to Keycloak discovery endpoint | Health check |
| 32 | `appsettings.json` + `appsettings.Development.json` | All required config keys documented |

### Wave 5: Database + Docker (Day 8)

| # | Task | Deliverable |
|---|------|-------------|
| 33 | `db/migrations/0001_create_customers_schema.sql` + `0002_create_customers_table.sql` | Customers schema |
| 34 | `db/migrations/0003_create_addresses_schema.sql` + `0004_create_addresses_table.sql` | Addresses schema |
| 35 | DbUp startup wiring in `Program.cs` | Auto-migration on app start |
| 36 | `docker-compose.yml` — API + PostgreSQL 16 + Keycloak 26.4 + OTEL Collector (Aspire Dashboard) | Full local dev stack |
| 37 | `keycloak/exemplar-realm.json` — exported realm (admin + user roles, exemplar-api client, audience mapper) | Reproducible KC config |
| 38 | `.env.example` — all required environment variable names with descriptions | Developer onboarding |
| 39 | `Dockerfile` — multi-stage (.NET 8 SDK build → runtime image) | Container build |

### Wave 6: Integration Tests + Polish (Day 9)

| # | Task | Deliverable |
|---|------|-------------|
| 40 | Keycloak TestContainers setup — real Keycloak container, real token acquisition | Real KC in tests |
| 41 | End-to-end: `docker compose up` → obtain JWT → call all 6 endpoints → verify responses | Runnable verification |
| 42 | Template annotation pass — inline comments marking parameterisation points | `/template-create` input |
| 43 | `README.md` — getting started, architecture overview, extension points (NATS, EF Core) | Developer guide |

---

## Findings

### Finding 1: `MapInboundClaims = false` is a common omission with Keycloak

Without `options.MapInboundClaims = false`, ASP.NET Core silently remaps Keycloak claim names (e.g. `realm_access`) to Microsoft XML URIs before any role-mapping code runs. The role parse finds nothing and roles are never added to the identity — a silent auth failure. The exemplar sets `MapInboundClaims = false` explicitly and documents why.

### Finding 2: Mid-chain `.IsSuccess` checks undermine railway-oriented programming

A common mistake is using imperative `if (result.IsSuccess)` checks between operations instead of chaining with `.Bind()`. This adds the cost of a typed error without the composability benefit — each check is a manual branch that could be composed automatically. The exemplar's `CustomerService` demonstrates clean `.Bind()`/`.BindAsync()`/`.Map()` chains with no mid-chain checks.

### Finding 3: OpenTelemetry configuration scope for templates

Production OTEL extensions that support multiple exporters (Elastic APM, LogicMonitor, Aspire Dashboard, generic OTLP) with lazy configuration and multi-layer fallback (~700 lines) are appropriate for enterprise use but would generate confusing templates. The exemplar simplifies to: Aspire Dashboard OTLP (local dev) + generic OTLP endpoint (CI/production). The template consumer can extend from there.

---

## Risks

### Risk 1: Keycloak realm JSON contains environment-specific secrets (Medium)

Exported realm JSON includes client secrets and internal Keycloak UUIDs. Committing it directly causes problems when applied to a fresh KC instance.

**Mitigation:** Commit a realm JSON with the client secret replaced by a placeholder (`REPLACE_ME_CLIENT_SECRET`). Document the one-time export process clearly in `README.md`. The `.env.example` carries the secret value used by docker-compose; the realm JSON carries the configuration structure.

### Risk 2: TestContainers Keycloak adds CI startup time (Low)

Keycloak container takes 20–40 seconds to become healthy. This affects CI pipeline duration.

**Mitigation:** Use `ICollectionFixture` so the Keycloak container is shared across all integration test classes in the collection — started once, not once per test class. Separate unit tests (fast) from integration tests (slower) in the CI pipeline so they can run in parallel stages.

---

## Accepted Findings Summary

| Decision | Resolution |
|----------|-----------|
| 1. Wrapper API | `Result<TError,TSuccess>` struct over CSharpFunctionalExtensions. `HandleResultAsync` uses `if/else` branching — no third-party types in the method body. Single-file C# 15 migration path. |
| 2. BaseError hierarchy | Abstract record, `InnerException` as init-only property (not virtual), optional `ErrorCode`. Pattern A (enum-discriminated) for Customers, Pattern B (simple records) for Addresses. Static factories on CustomerError for clean call sites. |
| 3. BC scope | Two BCs: Customers (primary, 4 endpoints) + Addresses (secondary, 2 endpoints, consumes `ICustomerLookup`). Generic universally understood domain. No MassTransit in MVE. |
| 4. Auth | Keycloak 26.4+ in all environments. `MapInboundClaims = false` explicitly set. TestContainers Keycloak in integration tests. 3 policies + AllowAnonymous demonstrated. |
| 5. Database | PostgreSQL 16 + Dapper + Npgsql. `NpgsqlDataSource` for connection pooling. DbUp pure-SQL migrations. TestContainers.PostgreSql for integration tests. |
| 6. Template readiness | 43-task plan across 6 waves, ~9 developer-days. 11 template parameters identified. All must-have features included. MassTransit documented as extension point only. |

---

## Architecture Score

| Dimension | Score | Notes |
|-----------|-------|-------|
| Domain neutrality | 10/10 | Generic Customers + Addresses — no niche business context |
| SOLID compliance | 9/10 | Clean interfaces, DI-registered, no static state, single responsibility |
| Railway-oriented composition | 9/10 | `.Bind()` chains throughout; no `.IsSuccess` mid-chain checks |
| AI code-gen friendliness | 9/10 | Small, conventional API surface on Result wrapper |
| Template-create value | 9/10 | Two BCs + inter-BC contract + auth + observability = rich scaffold |
| C# 15 migration readiness | 10/10 | Single-file wrapper change, zero call-site changes |

**Overall: 92/100**
