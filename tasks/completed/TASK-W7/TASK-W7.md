---
id: TASK-W7
title: "NATS Fleet Integration — capability discovery, domain events, request-reply with AI agents"
status: completed
task_type: feature
wave: 7
created: 2026-04-10T00:00:00Z
updated: 2026-04-10T00:00:00Z
completed: 2026-04-10T00:00:00Z
completed_location: tasks/completed/TASK-W7/
priority: high
complexity: 6
parent_review: TASK-59B3
feature_id: dotnet-fastendpoints-exemplar
dependencies: [TASK-W4, TASK-W5]
tags: [dotnet, nats, fleet, agent-manifest, capability-discovery, jetstream]
---

# TASK-W7: NATS Fleet Integration

## Scope

Add NATS client integration to the exemplar, demonstrating how a .NET API discovers and communicates with AI agents via the fleet's capability announcement system. This is the pattern that the FinProxy LPA platform (and any future .NET project) will use to interface with Python AI agents via NATS.

The integration reuses the same `AgentManifest` / `MessageEnvelope` / `Topics` patterns defined in the Python `nats-core` package, implemented as C# equivalents.

## Context

The fleet's Python agents (Architect Agent, Product Owner Agent, Behavioural Intelligence, etc.) publish `AgentManifest` to `fleet.register` on startup, declaring their `IntentCapability` and `ToolCapability`. The .NET platform subscribes to these announcements and builds a local registry, enabling runtime discovery of AI agents without hardcoded endpoints.

Two communication patterns are used:
- **Domain Events (JetStream)** — fire-and-forget events that AI agents react to asynchronously
- **Request-Reply (Core NATS)** — synchronous calls where .NET waits for an agent response with a timeout

Source reference: `nats-core/src/nats_core/manifest.py`, `topics.py`, `envelope.py`, `events/_agent.py`, `events/_fleet.py`

## Deliverables

| # | Task | Output |
|---|------|--------|
| 1 | `Exemplar.Fleet/` project — C# equivalents of nats-core types | `MessageEnvelope`, `EventType`, `AgentManifest`, `IntentCapability`, `ToolCapability`, `CommandPayload`, `ResultPayload`, `AgentHeartbeatPayload`, `AgentDeregistrationPayload` |
| 2 | `IManifestRegistry` + `InMemoryManifestRegistry` | `FindByIntent(intent)`, `FindByTool(toolName)`, `Get(agentId)`, `Register(manifest)`, `Deregister(agentId)` |
| 3 | `FleetDiscoveryService` (IHostedService) | Subscribes to `fleet.register`, `fleet.deregister`, `fleet.heartbeat.>`. Builds + maintains `ManifestRegistry`. Prunes stale agents. |
| 4 | `NatsEventPublisher` — domain event publisher | Publishes domain events from .NET BCs to NATS JetStream with `MessageEnvelope` wire format |
| 5 | `NatsAgentClient` — request-reply helper | `RequestAsync<TRequest, TResult>(agentId, command, args, timeout)` wrapping NATS request-reply with `CommandPayload`/`ResultPayload` and `Result<TError, TSuccess>` integration |
| 6 | `AgentUnavailableError : BaseError` | Functional error for when no agent is registered for a capability |
| 7 | DI registration extensions | `AddFleetIntegration(config)` — registers `INatsConnection`, `IManifestRegistry`, `FleetDiscoveryService`, `NatsEventPublisher`, `NatsAgentClient` |
| 8 | Docker Compose: add NATS server | `nats:latest` with `-js` flag for JetStream |
| 9 | Integration test: fleet discovery + request-reply | TestContainers with NATS container |
| 10 | Customers BC: publish `customer.created` domain event | Demonstrates domain event publishing from a real BC |
| 11 | Customers BC: call agent for enrichment (mock) | Demonstrates request-reply with `NatsAgentClient` through the service layer |

## Solution Structure Additions

```
src/
  Exemplar.Fleet/                    ← NEW project
    Models/
      MessageEnvelope.cs             ← Wire format (matches Python nats-core)
      EventType.cs                   ← Enum (matches Python nats-core)
      AgentManifest.cs               ← Capability declaration
      IntentCapability.cs
      ToolCapability.cs
      CommandPayload.cs
      ResultPayload.cs
      AgentHeartbeatPayload.cs
      AgentDeregistrationPayload.cs
    Registry/
      IManifestRegistry.cs
      InMemoryManifestRegistry.cs
    Services/
      FleetDiscoveryService.cs       ← IHostedService: subscribe to fleet.*
      NatsEventPublisher.cs          ← Publish domain events to JetStream
      NatsAgentClient.cs             ← Request-reply with Result<> integration
    Errors/
      AgentUnavailableError.cs       ← BaseError subclass
    Extensions/
      FleetServiceCollectionExtensions.cs
  Exemplar.Customers/
    Services/
      CustomerService.cs             ← MODIFIED: publishes customer.created event
                                     ← MODIFIED: calls agent via NatsAgentClient
tests/
  Exemplar.Fleet.Tests/              ← NEW test project
    ManifestRegistryTests.cs
    MessageEnvelopeTests.cs
    FleetDiscoveryServiceTests.cs
    NatsAgentClientTests.cs
    NatsIntegrationTests.cs          ← TestContainers with real NATS
```

## Key Implementation Rules

### C# fleet types must match Python nats-core wire format exactly

The JSON produced by `System.Text.Json` for `MessageEnvelope` must deserialise correctly in Python and vice versa. Field names use `snake_case` via `JsonPropertyName` attributes or a naming policy:

```csharp
public record MessageEnvelope
{
    [JsonPropertyName("message_id")]
    public string MessageId { get; init; } = Guid.NewGuid().ToString();

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0";

    [JsonPropertyName("source_id")]
    public required string SourceId { get; init; }

    [JsonPropertyName("event_type")]
    public required EventType EventType { get; init; }

    [JsonPropertyName("project")]
    public string? Project { get; init; }

    [JsonPropertyName("correlation_id")]
    public string? CorrelationId { get; init; }

    [JsonPropertyName("payload")]
    public required JsonElement Payload { get; init; }
}
```

### AgentManifest uses the same field names and constraints as Python

```csharp
public record AgentManifest
{
    [JsonPropertyName("agent_id")]
    public required string AgentId { get; init; }  // kebab-case enforced

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("version")]
    public string Version { get; init; } = "0.1.0";

    [JsonPropertyName("intents")]
    public List<IntentCapability> Intents { get; init; } = [];

    [JsonPropertyName("tools")]
    public List<ToolCapability> Tools { get; init; } = [];

    [JsonPropertyName("template")]
    public required string Template { get; init; }

    [JsonPropertyName("max_concurrent")]
    public int MaxConcurrent { get; init; } = 1;

    [JsonPropertyName("status")]
    public string Status { get; init; } = "ready";

    [JsonPropertyName("trust_tier")]
    public string TrustTier { get; init; } = "specialist";
}
```

### NatsAgentClient integrates with the Result<TError, TSuccess> pattern

```csharp
public async Task<Result<AgentUnavailableError, TResult>> RequestAsync<TResult>(
    string toolName,
    string command,
    object args,
    TimeSpan? timeout = null)
{
    var agents = _registry.FindByTool(toolName);
    if (agents.Count == 0)
        return new AgentUnavailableError(toolName);

    var agent = agents.First();
    var subject = $"agents.command.{agent.AgentId}";
    var cmd = new CommandPayload
    {
        Command = command,
        Args = args,
        CorrelationId = Guid.NewGuid().ToString()
    };

    var reply = await _nats.RequestAsync<CommandPayload, ResultPayload>(
        subject, cmd, timeout ?? TimeSpan.FromSeconds(30));

    return JsonSerializer.Deserialize<TResult>(reply.Data.Result);
}
```

### AgentUnavailableError follows the existing BaseError pattern

```csharp
public record AgentUnavailableError : BaseError
{
    public string ToolName { get; }

    public AgentUnavailableError(string toolName)
        : base($"No agent registered for tool '{toolName}'")
    {
        ToolName = toolName;
        ErrorCode = "AGENT_UNAVAILABLE";
    }

    public override int StatusCode => StatusCodes.Status503ServiceUnavailable;
}
```

### Domain event publishing from service layer (not endpoint)

```csharp
// In CustomerService.cs — after successful customer creation
public async Task<Result<CustomerError, CustomerDto>> CreateAsync(CreateCustomerRequest request)
{
    return await _validator.Validate(request)
        .Bind(validated => _repository.InsertAsync(validated))
        .Map(customer => customer.ToDto())
        .Tap(dto => _eventPublisher.PublishAsync(
            "customers.created",
            EventType.COMMAND,  // or a domain-specific event type
            new { CustomerId = dto.Id, dto.Name }));
}
```

## NuGet References (Exemplar.Fleet)

```xml
<PackageReference Include="NATS.Net" Version="2.*" />
<PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" Version="*" />
```

Plus `Exemplar.Fleet` references `Exemplar.Core` (for `BaseError`, `Result<>`).

## Docker Compose Addition

```yaml
  nats:
    image: nats:latest
    ports:
      - "4222:4222"   # Client
      - "8222:8222"   # Monitoring
    command: ["-js"]   # Enable JetStream
    healthcheck:
      test: ["CMD", "nats-server", "--signal", "ldm"]
      interval: 5s
      timeout: 3s
      retries: 3
```

## Quality Gates

- [ ] `dotnet build` — zero warnings, zero errors
- [ ] `dotnet test Exemplar.Fleet.Tests` — 100% pass
- [ ] `MessageEnvelope` JSON round-trips correctly between C# and Python (cross-language test)
- [ ] `InMemoryManifestRegistry`: register, deregister, find by intent (glob match), find by tool
- [ ] `FleetDiscoveryService` starts, subscribes, processes registration, prunes stale agents
- [ ] `NatsAgentClient` returns `Result.Failure<AgentUnavailableError>` when no agent registered
- [ ] `NatsAgentClient` returns `Result.Success` when mock agent responds
- [ ] `CustomerService.CreateAsync` publishes domain event to NATS
- [ ] Integration test with TestContainers NATS: full fleet discovery → request-reply cycle
- [ ] No reference to specific agent IDs or tool names hardcoded outside of test fixtures

## Template Parameterisation

4 additional template points for W7:

```csharp
// {{TEMPLATE: NatsUrl}} — default "nats://localhost:4222"
// {{TEMPLATE: FleetSourceId}} — the .NET service's source_id in MessageEnvelope
// {{TEMPLATE: JetStreamEnabled}} — whether to enable JetStream streams
// {{TEMPLATE: HeartbeatTimeoutSeconds}} — agent liveness timeout, default 90
```

## Relationship to lpa-platform

This exemplar wave creates the reusable pattern. When the lpa-platform is built (via `/template-create` from this exemplar), the `Exemplar.Fleet` project becomes `FinProxy.Fleet`, and the patterns are instantiated with real agent tool names (`lpa_instruction_parse`, `behavioural_baseline_compute`, `anomaly_score`, `architect_feasibility`).

## Next Steps After W7

- `/template-create` extracts the full exemplar (W1-W7) into the `dotnet-fastendpoints` GuardKit template
- `lpa-platform` is scaffolded from the template
- The fleet types in `FinProxy.Fleet` are tested against real Python agents running on NATS
- If the C# fleet types prove reusable beyond FinProxy, extract into a `nats-core-dotnet` NuGet package

## Previous Wave

TASK-W6 (Integration Tests + Polish) must be complete before W7.
