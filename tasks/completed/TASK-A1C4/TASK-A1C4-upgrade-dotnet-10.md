---
id: TASK-A1C4
title: Upgrade project from .NET 8 to .NET 10 LTS
status: completed
task_type: feature
created: 2026-04-10T00:00:00Z
updated: 2026-04-11T00:00:00Z
completed: 2026-04-11T00:00:00Z
priority: normal
tags: [dotnet, upgrade, lts, dependencies]
complexity: 3
test_results:
  status: passed
  coverage: null
  last_run: 2026-04-10
---

# Task: Upgrade Project from .NET 8 to .NET 10 LTS

## Description

The project currently targets .NET 8 LTS (support ends November 2026). .NET 10 was released November 11, 2025 and is the current LTS release with support through November 2028. Upgrade all projects in the solution to target `net10.0`.

## Background

| | .NET 8 | .NET 10 |
|---|---|---|
| Released | November 2023 | November 2025 |
| LTS End | November 2026 | November 2028 |
| C# Version | C# 12 | C# 14 |

Key improvements in .NET 10 relevant to this project:
- Significant runtime performance gains (up to 2x in some benchmarks)
- C# 14 features: field-backed properties, `ref`/`in`/`out` in lambda expressions
- Improved JSON serialisation options (duplicate property detection, `PipeReader` support)
- Post-quantum cryptography additions

## Scope

All `.csproj` files in the solution:
- `src/Exemplar.Core/Exemplar.Core.csproj`
- `src/Exemplar.API/Exemplar.API.csproj`
- Any test projects under `tests/`

## Acceptance Criteria

- [x] All `.csproj` files updated from `net8.0` to `net10.0`
- [x] All NuGet package dependencies reviewed and updated to .NET 10-compatible versions
- [x] Solution builds without errors or warnings introduced by the upgrade
- [x] All existing tests pass after upgrade
- [x] `global.json` (if present) updated to pin .NET 10 SDK
- [x] Docker base images updated from `mcr.microsoft.com/dotnet/aspnet:8.0` to `10.0` (if applicable)
- [x] No breaking changes introduced by the upgrade that require code changes beyond the TFM bump

## Implementation Notes

Upgrade steps:
1. Update `<TargetFramework>net8.0</TargetFramework>` → `net10.0` in all `.csproj` files
2. Run `dotnet restore` and resolve any package compatibility issues
3. Check for any APIs removed or changed between .NET 8 and .NET 10 (use `dotnet-compatibility` tool if needed)
4. Update Docker base images if a `Dockerfile` or `docker-compose.yml` references .NET 8 images
5. Run full build and test suite to verify

## Test Execution Log

**2026-04-10** — SDK 10.0.101, `dotnet build` → 0 errors, 0 warnings

| Test Assembly | Result | Tests |
|---|---|---|
| Exemplar.Core.Tests | ✅ Passed | 14/14 |
| Exemplar.Addresses.Tests | ✅ Passed | 15/15 |
| Exemplar.Customers.Tests | ✅ Passed | 20/20 |
| Exemplar.Fleet.Tests | ✅ Passed | 37/37 |
| Exemplar.E2E.Tests | skipped (requires live infra) | — |

Also fixed a pre-existing bug in `NatsIntegrationTests.cs`: `UntilInternalTcpPortIsAvailable(4222)`
executes `/bin/sh` inside the container, but `nats:latest` is a scratch image with no shell.
Replaced with `UntilMessageIsLogged("Server is ready")` which reads stdout instead.
