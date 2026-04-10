using System.Text.Json.Serialization;

namespace Exemplar.Fleet.Models;

/// <summary>
/// Capability declaration published by an agent on startup.
/// Field names and constraints match Python nats-core AgentManifest exactly.
/// agent_id must be kebab-case (enforced by the publishing agent).
/// </summary>
public record AgentManifest
{
    [JsonPropertyName("agent_id")]
    public required string AgentId { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("version")]
    public string Version { get; init; } = "0.1.0";

    [JsonPropertyName("intents")]
    public IReadOnlyList<IntentCapability> Intents { get; init; } = [];

    [JsonPropertyName("tools")]
    public IReadOnlyList<ToolCapability> Tools { get; init; } = [];

    [JsonPropertyName("template")]
    public required string Template { get; init; }

    [JsonPropertyName("max_concurrent")]
    public int MaxConcurrent { get; init; } = 1;

    [JsonPropertyName("status")]
    public string Status { get; init; } = "ready";

    [JsonPropertyName("trust_tier")]
    public string TrustTier { get; init; } = "specialist";
}
