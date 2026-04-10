namespace Exemplar.Fleet;

/// <summary>
/// Configuration for fleet NATS integration.
/// Template parameters are annotated with {{TEMPLATE: ...}} comments.
/// </summary>
public sealed class FleetOptions
{
    /// <summary>{{TEMPLATE: NatsUrl}} — NATS server URL.</summary>
    public string NatsUrl { get; set; } = "nats://localhost:4222";

    /// <summary>{{TEMPLATE: FleetSourceId}} — source_id stamped on all outbound MessageEnvelope payloads.</summary>
    public string SourceId { get; set; } = "exemplar-api";

    /// <summary>{{TEMPLATE: JetStreamEnabled}} — set false to fall back to Core NATS for domain events.</summary>
    public bool JetStreamEnabled { get; set; } = true;

    /// <summary>{{TEMPLATE: HeartbeatTimeoutSeconds}} — seconds before a non-heartbeating agent is pruned.</summary>
    public int HeartbeatTimeoutSeconds { get; set; } = 90;
}
