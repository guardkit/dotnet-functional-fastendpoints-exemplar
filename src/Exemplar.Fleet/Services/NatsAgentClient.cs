using System.Text.Json;
using Exemplar.Core.Functional;
using Exemplar.Fleet.Errors;
using Exemplar.Fleet.Models;
using Exemplar.Fleet.Registry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.Core;

namespace Exemplar.Fleet.Services;

/// <summary>
/// Sends request-reply commands to fleet agents via Core NATS.
/// Resolves the agent from <see cref="IManifestRegistry"/> by tool name, then
/// publishes a <see cref="CommandPayload"/> to agents.command.{agent_id} and
/// awaits a <see cref="ResultPayload"/> reply.
/// </summary>
public sealed class NatsAgentClient : INatsAgentClient
{
    private readonly INatsConnection _nats;
    private readonly IManifestRegistry _registry;
    private readonly FleetOptions _opts;
    private readonly ILogger<NatsAgentClient> _logger;

    public NatsAgentClient(
        INatsConnection nats,
        IManifestRegistry registry,
        IOptions<FleetOptions> opts,
        ILogger<NatsAgentClient> logger)
    {
        _nats = nats;
        _registry = registry;
        _opts = opts.Value;
        _logger = logger;
    }

    public async Task<Result<AgentUnavailableError, TResult>> RequestAsync<TResult>(
        string toolName,
        string command,
        object args,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        var agents = _registry.FindByTool(toolName);
        if (agents.Count == 0)
        {
            _logger.LogWarning("No agent registered for tool '{ToolName}'", toolName);
            return new AgentUnavailableError(toolName);
        }

        var agent = agents[0];
        var subject = $"agents.command.{agent.AgentId}";
        var cmd = new CommandPayload
        {
            Command = command,
            Args = JsonSerializer.SerializeToElement(args),
        };

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linkedCts.CancelAfter(timeout ?? TimeSpan.FromSeconds(30));

        try
        {
            var reply = await _nats.RequestAsync<CommandPayload, ResultPayload>(
                subject, cmd, cancellationToken: linkedCts.Token);

            var result = reply.Data;
            if (result is null || !result.IsSuccess)
            {
                var errorMsg = result?.Error ?? "Agent returned null or failed result";
                _logger.LogWarning("Agent {AgentId} returned failure for tool '{ToolName}': {Error}",
                    agent.AgentId, toolName, errorMsg);
                return new AgentUnavailableError(toolName);
            }

            var deserialized = JsonSerializer.Deserialize<TResult>(result.Result);
            if (deserialized is null)
                return new AgentUnavailableError(toolName);

            return Result<AgentUnavailableError, TResult>.Success(deserialized);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Request to agent {AgentId} for tool '{ToolName}' timed out",
                agent.AgentId, toolName);
            return new AgentUnavailableError(toolName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Request to agent {AgentId} for tool '{ToolName}' failed",
                agent.AgentId, toolName);
            return new AgentUnavailableError(toolName);
        }
    }
}
