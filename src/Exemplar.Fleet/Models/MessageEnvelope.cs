using System.Text.Json;
using System.Text.Json.Serialization;

namespace Exemplar.Fleet.Models;

/// <summary>
/// Universal wire format for all NATS messages in the fleet.
/// JSON field names use snake_case to match Python nats-core exactly.
/// Payload is stored as a raw JsonElement so the envelope can be deserialized
/// without knowing the concrete payload type upfront.
/// </summary>
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
