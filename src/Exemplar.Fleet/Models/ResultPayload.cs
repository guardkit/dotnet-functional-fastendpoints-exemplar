using System.Text.Json;
using System.Text.Json.Serialization;

namespace Exemplar.Fleet.Models;

/// <summary>
/// Payload returned by an agent in response to a CommandPayload.
/// Maps to Python nats-core ResultPayload.
/// </summary>
public record ResultPayload
{
    [JsonPropertyName("correlation_id")]
    public string? CorrelationId { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = "success";

    [JsonPropertyName("result")]
    public JsonElement Result { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    public bool IsSuccess => Status == "success";
}
