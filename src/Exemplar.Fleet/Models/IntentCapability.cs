using System.Text.Json.Serialization;

namespace Exemplar.Fleet.Models;

/// <summary>
/// Declares a high-level intent that an agent can fulfil.
/// Maps to Python nats-core IntentCapability.
/// </summary>
public record IntentCapability
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;
}
