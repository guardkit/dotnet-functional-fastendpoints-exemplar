using Exemplar.Fleet.Models;
using Exemplar.Fleet.Registry;
using Exemplar.Fleet.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NSubstitute;
using Xunit;

namespace Exemplar.Fleet.Tests;

public class NatsAgentClientTests
{
    private readonly IManifestRegistry _registry = Substitute.For<IManifestRegistry>();
    private readonly INatsConnection _nats = Substitute.For<INatsConnection>();
    private readonly NatsAgentClient _sut;

    public NatsAgentClientTests()
    {
        _sut = new NatsAgentClient(
            _nats,
            _registry,
            Options.Create(new FleetOptions()),
            NullLogger<NatsAgentClient>.Instance);
    }

    // ── No agent registered ────────────────────────────────────────────────────

    [Fact]
    public async Task RequestAsync_WhenNoAgentRegisteredForTool_ReturnsAgentUnavailableError()
    {
        _registry.FindByTool("unknown_tool").Returns([]);

        var result = await _sut.RequestAsync<object>("unknown_tool", "cmd", new { });

        result.IsFailure.Should().BeTrue();
        result.Error.ToolName.Should().Be("unknown_tool");
        result.Error.ErrorCode.Should().Be("AGENT_UNAVAILABLE");
        result.Error.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task RequestAsync_WhenNoAgentRegistered_DoesNotCallNats()
    {
        _registry.FindByTool("any_tool").Returns([]);

        await _sut.RequestAsync<object>("any_tool", "cmd", new { });

        await _nats.DidNotReceive()
            .RequestAsync<object, object>(Arg.Any<string>(), Arg.Any<object>());
    }

    // ── Error type validation ──────────────────────────────────────────────────

    [Fact]
    public async Task AgentUnavailableError_ContainsToolNameInMessage()
    {
        _registry.FindByTool("my_tool").Returns([]);

        var result = await _sut.RequestAsync<object>("my_tool", "cmd", new { });

        result.Error.Message.Should().Contain("my_tool");
    }
}
