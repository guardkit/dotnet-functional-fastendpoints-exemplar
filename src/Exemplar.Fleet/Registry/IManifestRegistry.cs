using Exemplar.Fleet.Models;

namespace Exemplar.Fleet.Registry;

/// <summary>
/// Runtime registry of agent manifests received via fleet.register.
/// All methods must be thread-safe.
/// </summary>
public interface IManifestRegistry
{
    /// <summary>Registers or replaces the manifest for the given agent.</summary>
    void Register(AgentManifest manifest);

    /// <summary>Removes the agent from the registry. No-op if not present.</summary>
    void Deregister(string agentId);

    /// <summary>Returns the manifest for the given agent, or null if not registered.</summary>
    AgentManifest? Get(string agentId);

    /// <summary>
    /// Returns all agents whose <see cref="Models.IntentCapability.Name"/> matches <paramref name="intentPattern"/>.
    /// Supports simple wildcard '*' matching (e.g. "customer.*" matches "customer.enrich").
    /// </summary>
    IReadOnlyList<AgentManifest> FindByIntent(string intentPattern);

    /// <summary>Returns all agents that declare a tool with the exact given name.</summary>
    IReadOnlyList<AgentManifest> FindByTool(string toolName);

    /// <summary>Returns all currently registered agents.</summary>
    IReadOnlyList<AgentManifest> All();
}
