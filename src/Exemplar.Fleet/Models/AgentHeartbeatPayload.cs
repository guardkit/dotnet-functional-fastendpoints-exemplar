using System.Text.Json.Serialization;

namespace Exemplar.Fleet.Models;

/// <summary>
/// Published by agents periodically to signal liveness.
/// Received on fleet.heartbeat.{agent_id}.
/// Maps to Python nats-core AgentHeartbeatPayload.
/// </summary>
public record AgentHeartbeatPayload
{
    [JsonPropertyName("agent_id")]
    public required string AgentId { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    [JsonPropertyName("status")]
    public string Status { get; init; } = "ready";
}
