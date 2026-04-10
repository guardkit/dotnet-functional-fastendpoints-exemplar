---
id: TASK-W4
title: "API Host — Program.cs, Keycloak auth, DbUp, appsettings"
status: backlog
task_type: feature
wave: 4
created: 2026-04-10T00:00:00Z
updated: 2026-04-10T00:00:00Z
priority: high
complexity: 5
parent_review: TASK-59B3
feature_id: dotnet-fastendpoints-exemplar
dependencies: [TASK-W1, TASK-W2, TASK-W3]
tags: [dotnet, fastendpoints, auth, keycloak, program]
---

# TASK-W4: API Host

## Scope

Wire everything together in `Exemplar.API`: composition root (`Program.cs`), Keycloak JWT auth, DbUp migration runner on startup, health checks, and configuration files.

## Deliverables

| # | Task | Output |
|---|------|--------|
| 29 | `Program.cs` — registration order: OTEL → Serilog → Auth → DB → Repositories → Services → FastEndpoints → HealthChecks → DbUp | Composition root |
| 30 | `AuthenticationExtensions.cs` — `MapInboundClaims = false`, role mapping, 3 policies | Keycloak auth |
| 31 | `KeycloakHealthCheck.cs` — HTTP GET to Keycloak OIDC discovery endpoint | Health check |
| 32 | `appsettings.json` + `appsettings.Development.json` | All required config keys, documented |

## Key Implementation Rules

### Program.cs — registration order matters
```csharp
var builder = WebApplication.CreateBuilder(args);

// 1. Observability first (captures startup errors)
builder.AddOpenTelemetry();
builder.AddSerilog();

// 2. Auth
builder.Services.AddAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddAuthorizationPolicies();

// 3. Database
builder.Services.AddNpgsqlDataSource(connectionString);

// 4. Repositories and Services
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ICustomerLookup>(sp => sp.GetRequiredService<ICustomerService>());
builder.Services.AddScoped<IAddressRepository, AddressRepository>();
builder.Services.AddScoped<IAddressService, AddressService>();

// 5. FastEndpoints
builder.Services.AddFastEndpointsServices();

// 6. Health checks
builder.Services.AddHealthChecks()
    .AddCheck<KeycloakHealthCheck>("keycloak")
    .AddNpgSql(connectionString, name: "postgresql");

var app = builder.Build();

// 7. DbUp migrations (before accepting traffic)
app.RunDbMigrations();

app.UseApiConfiguration();
app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready");
app.Run();
```

### AuthenticationExtensions — MapInboundClaims = false is mandatory
```csharp
public static IServiceCollection AddAuthentication(
    this IServiceCollection services,
    IConfiguration configuration,
    IWebHostEnvironment environment)
{
    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = configuration["Authentication:Authority"];
            options.Audience  = configuration["Authentication:Audience"];
            options.MapInboundClaims = false;  // REQUIRED: preserve Keycloak claim names
            options.RequireHttpsMetadata = !environment.IsDevelopment();
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = true,
                ValidateIssuer   = true,
                NameClaimType    = "preferred_username",
                RoleClaimType    = ClaimTypes.Role
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = ProcessKeycloakRoles
            };
        });
    return services;
}

// Role mapping: realm_access.roles + resource_access.{client}.roles → ClaimTypes.Role
private static Task ProcessKeycloakRoles(TokenValidatedContext ctx) { ... }
```

### Authorization policies
```csharp
services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly",           p => p.RequireRole("admin"))
    .AddPolicy("UserOrAdmin",         p => p.RequireRole("user", "admin"))
    .AddPolicy("RequireAuthentication", p => p.RequireAuthenticatedUser());
```

### appsettings.json — all keys documented
```json
{
  "Authentication": {
    "Authority": "",   // Keycloak realm URL e.g. http://localhost:8080/realms/exemplar
    "Audience":  "",   // Client ID e.g. exemplar-api
    "ClientId":  ""    // Same as Audience for Keycloak
  },
  "ConnectionStrings": {
    "DefaultConnection": ""
  },
  "OpenTelemetry": {
    "Endpoint": ""     // OTLP endpoint e.g. http://localhost:4317
  },
  "FastEndpoints": {
    "RoutePrefix": "api",
    "VersionPrefix": "v"
  }
}
```

## Quality Gates

- [ ] `dotnet build` — zero warnings, zero errors
- [ ] Application starts and `/health/live` returns 200
- [ ] `MapInboundClaims = false` present and commented
- [ ] `ICustomerLookup` is resolved from the same `CustomerService` singleton as `ICustomerService` (no double-instantiation)
- [ ] DbUp runs migrations on startup and is idempotent (safe to run twice)

## Next Wave

TASK-W5 (Database + Docker) adds migrations SQL, docker-compose, Keycloak realm config.
