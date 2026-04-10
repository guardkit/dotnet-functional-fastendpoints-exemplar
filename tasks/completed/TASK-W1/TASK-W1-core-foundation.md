---
id: TASK-W1
title: "Core Foundation — Result wrapper, BaseError, endpoint extensions, OTEL/Serilog"
status: completed
task_type: feature
wave: 1
created: 2026-04-10T00:00:00Z
updated: 2026-04-10T12:00:00Z
completed: 2026-04-10T12:00:00Z
priority: high
complexity: 5
parent_review: TASK-59B3
feature_id: dotnet-fastendpoints-exemplar
dependencies: []
tags: [dotnet, fastendpoints, csharpfunctionalextensions, core]
previous_state: backlog
state_transition_reason: "Automatic transition for task-work execution"
completed_location: tasks/completed/TASK-W1/
quality_gates:
  build: passed
  tests: "14/14"
  warnings: 0
  errors: 0
---

# TASK-W1: Core Foundation

## Scope

Build `Exemplar.Core` — the shared library that all bounded context projects reference. No domain logic here; only cross-cutting infrastructure types.

## Deliverables

| # | Task | Output |
|---|------|--------|
| 1 | Scaffold solution — `.sln`, all `.csproj` files, `global.json` (net8.0) | `Exemplar.sln` + 6 projects |
| 2 | `BaseError.cs` + `CommonErrors.cs` | Abstract record + cross-cutting errors |
| 3 | `Result<TError, TSuccess>.cs` | Thin wrapper over CSharpFunctionalExtensions |
| 4 | `ResultExtensions.cs` — `HandleResultAsync` + `HandleErrorAsync` | Endpoint bridge |
| 5 | `FastEndpointsExtensions.cs` — `AddFastEndpointsServices` + `UseApiConfiguration` | ProblemDetails, route prefix, camelCase |
| 6 | `OpenTelemetryExtensions.cs` — Aspire Dashboard + generic OTLP only | OTEL wiring |
| 7 | `SerilogExtensions.cs` — structured logging with OTel sink | Serilog wiring |
| 8 | `HealthCheckExtensions.cs` | Liveness + readiness probe helpers |
| 9 | `Exemplar.Core.Tests/` — Result wrapper unit tests | Map, Bind, BindAsync, Match, implicit conversions, failure short-circuits |

## Solution Structure to Create

```
Exemplar.sln
src/
  Exemplar.Core/
    Functional/
      Result.cs
      ResultExtensions.cs
    Errors/
      BaseError.cs
      CommonErrors.cs
    Endpoints/
      EndpointResultExtensions.cs
      FastEndpointsExtensions.cs
    Infrastructure/
      OpenTelemetryExtensions.cs
      SerilogExtensions.cs
      HealthCheckExtensions.cs
  Exemplar.Customers/           (empty, created for project reference setup)
  Exemplar.Customers.Contracts/ (empty)
  Exemplar.Addresses/           (empty)
  Exemplar.API/                 (empty)
tests/
  Exemplar.Core.Tests/
```

## Key Implementation Rules

### Result<TError, TSuccess> — error-first, wraps CSharpFunctionalExtensions
```csharp
public readonly struct Result<TError, TSuccess> where TError : BaseError
{
    private readonly CSharpFunctionalExtensions.Result<TSuccess, TError> _inner;
    // error-first type ordering at the wrapper surface
    // CSharpFunctionalExtensions is internal — consuming code never references it directly
}
```

### BaseError — StatusCode virtual, InnerException init-only (NOT virtual)
```csharp
public abstract record BaseError(string Message)
{
    public virtual int StatusCode => StatusCodes.Status500InternalServerError;
    public string? ErrorCode { get; init; }
    public Exception? InnerException { get; init; }  // init-only — set via static factories
}
```

### HandleResultAsync — if/else, NOT MatchAsync
```csharp
// Both branches are void async — avoids needing a shared return type
if (result.IsSuccess)
    await onSuccess(result.Value);
else
    await endpoint.HandleErrorAsync(result.Error, ct);
```

### OpenTelemetry — simplified scope (no Elastic APM, no LogicMonitor)
- Aspire Dashboard OTLP for local dev
- Generic OTLP endpoint for CI/production
- ASP.NET Core + HttpClient + Npgsql instrumentation

## NuGet References (Exemplar.Core)

```xml
<PackageReference Include="CSharpFunctionalExtensions" Version="*" />
<PackageReference Include="FastEndpoints" Version="*" />
<PackageReference Include="FastEndpoints.Swagger" Version="*" />
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="*" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="*" />
<PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="*" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="*" />
<PackageReference Include="Serilog.AspNetCore" Version="*" />
<PackageReference Include="Serilog.Sinks.OpenTelemetry" Version="*" />
```

## Quality Gates

- [x] `dotnet build` — zero warnings, zero errors
- [x] `dotnet test Exemplar.Core.Tests` — 14/14 pass
- [x] Result wrapper: Success path, Failure path, Bind short-circuits on failure, Map transforms success, implicit conversions work
- [x] No reference to CSharpFunctionalExtensions in any project other than `Exemplar.Core`

## Next Wave

TASK-W2 (Customers BC) depends on this wave being complete and green.
