using System.Text.Json;
using System.Text.Json.Serialization;

namespace Exemplar.Fleet.Models;

/// <summary>
/// Payload sent to an agent via NATS request-reply on subject agents.command.{agent_id}.
/// Maps to Python nats-core CommandPayload.
/// </summary>
public record CommandPayload
{
    [JsonPropertyName("command")]
    public required string Command { get; init; }

    [JsonPropertyName("args")]
    public required JsonElement Args { get; init; }

    [JsonPropertyName("correlation_id")]
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString();
}
