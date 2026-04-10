using Exemplar.Core.Functional;
using Exemplar.Fleet.Errors;

namespace Exemplar.Fleet.Services;

/// <summary>
/// Sends request-reply messages to fleet agents discovered via <see cref="Registry.IManifestRegistry"/>.
/// Uses Core NATS request-reply with <see cref="Models.CommandPayload"/> / <see cref="Models.ResultPayload"/> wire format.
/// </summary>
public interface INatsAgentClient
{
    /// <summary>
    /// Finds an agent registered for <paramref name="toolName"/> and sends a command, waiting for a reply.
    /// Returns <see cref="AgentUnavailableError"/> if no agent is registered or the request times out.
    /// </summary>
    Task<Result<AgentUnavailableError, TResult>> RequestAsync<TResult>(
        string toolName,
        string command,
        object args,
        TimeSpan? timeout = null,
        CancellationToken ct = default);
}
