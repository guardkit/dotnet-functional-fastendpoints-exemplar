using System.Text.Json.Serialization;

namespace Exemplar.Fleet.Models;

/// <summary>
/// Published by an agent (or the fleet) when an agent is shutting down.
/// Received on fleet.deregister.
/// Maps to Python nats-core AgentDeregistrationPayload.
/// </summary>
public record AgentDeregistrationPayload
{
    [JsonPropertyName("agent_id")]
    public required string AgentId { get; init; }

    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}
