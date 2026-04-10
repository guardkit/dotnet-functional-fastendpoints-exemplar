using Exemplar.Core.Errors;
using Microsoft.AspNetCore.Http;

namespace Exemplar.Fleet.Errors;

/// <summary>
/// Returned when no agent is registered that can handle the requested tool or intent.
/// </summary>
public record AgentUnavailableError(string ToolName)
    : BaseError($"No agent registered for tool '{ToolName}'")
{
    public override int StatusCode => StatusCodes.Status503ServiceUnavailable;
    public override string? ErrorCode => "AGENT_UNAVAILABLE";
}
