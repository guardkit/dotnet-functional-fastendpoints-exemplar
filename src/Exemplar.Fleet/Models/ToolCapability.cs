using System.Text.Json;
using System.Text.Json.Serialization;

namespace Exemplar.Fleet.Models;

/// <summary>
/// Declares a named tool that an agent exposes for request-reply calls.
/// Maps to Python nats-core ToolCapability.
/// </summary>
public record ToolCapability
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("input_schema")]
    public JsonElement? InputSchema { get; init; }
}
