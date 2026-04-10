using System.Text.Json;
using Exemplar.Fleet.Models;
using Exemplar.Fleet.Registry;
using Exemplar.Fleet.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Exemplar.Fleet.Tests;

/// <summary>
/// Tests the message-handler logic of <see cref="FleetDiscoveryService"/> in isolation.
/// NATS subscription and heartbeat pruning are covered by NatsIntegrationTests.
/// </summary>
public class FleetDiscoveryServiceTests
{
    private readonly IManifestRegistry _registry = Substitute.For<IManifestRegistry>();
    private readonly FleetDiscoveryService _sut;

    public FleetDiscoveryServiceTests()
    {
        var nats = Substitute.For<NATS.Client.Core.INatsConnection>();
        var opts = Options.Create(new FleetOptions { HeartbeatTimeoutSeconds = 90 });
        _sut = new FleetDiscoveryService(nats, _registry, opts, NullLogger<FleetDiscoveryService>.Instance);
    }

    // ── HandleRegistration ─────────────────────────────────────────────────────

    [Fact]
    public void HandleRegistration_ValidEnvelope_RegistersManifest()
    {
        var manifest = MakeManifest("arch-agent");
        var envelope = WrapInEnvelope(manifest, EventType.Register);

        _sut.HandleRegistration(envelope);

        _registry.Received(1).Register(Arg.Is<AgentManifest>(m => m.AgentId == "arch-agent"));
    }

    [Fact]
    public void HandleRegistration_InvalidPayload_DoesNotThrow()
    {
        var envelope = new MessageEnvelope
        {
            SourceId = "bad-agent",
            EventType = EventType.Register,
            Payload = JsonSerializer.SerializeToElement("not-a-manifest"),
        };

        var act = () => _sut.HandleRegistration(envelope);
        act.Should().NotThrow();
        _registry.DidNotReceive().Register(Arg.Any<AgentManifest>());
    }

    // ── HandleDeregistration ───────────────────────────────────────────────────

    [Fact]
    public void HandleDeregistration_ValidEnvelope_DeregistersAgent()
    {
        var payload = new AgentDeregistrationPayload { AgentId = "old-agent", Reason = "shutdown" };
        var envelope = WrapInEnvelope(payload, EventType.Deregister);

        _sut.HandleDeregistration(envelope);

        _registry.Received(1).Deregister("old-agent");
    }

    [Fact]
    public void HandleDeregistration_InvalidPayload_DoesNotThrow()
    {
        var envelope = new MessageEnvelope
        {
            SourceId = "bad-source",
            EventType = EventType.Deregister,
            Payload = JsonSerializer.SerializeToElement(42),
        };

        var act = () => _sut.HandleDeregistration(envelope);
        act.Should().NotThrow();
        _registry.DidNotReceive().Deregister(Arg.Any<string>());
    }

    // ── HandleHeartbeat ────────────────────────────────────────────────────────

    [Fact]
    public void HandleHeartbeat_ValidEnvelope_DoesNotThrow()
    {
        var payload = new AgentHeartbeatPayload { AgentId = "healthy-agent", Status = "ready" };
        var envelope = WrapInEnvelope(payload, EventType.Heartbeat);

        var act = () => _sut.HandleHeartbeat(envelope);
        act.Should().NotThrow();
    }

    [Fact]
    public void HandleHeartbeat_AfterRegister_AgentRemainsRegistered()
    {
        // Use real registry to verify heartbeat doesn't deregister anything.
        var realRegistry = new InMemoryManifestRegistry();
        var nats = Substitute.For<NATS.Client.Core.INatsConnection>();
        var opts = Options.Create(new FleetOptions());
        var sut = new FleetDiscoveryService(nats, realRegistry, opts, NullLogger<FleetDiscoveryService>.Instance);

        var manifest = MakeManifest("live-agent");
        sut.HandleRegistration(WrapInEnvelope(manifest, EventType.Register));
        sut.HandleHeartbeat(WrapInEnvelope(
            new AgentHeartbeatPayload { AgentId = "live-agent" }, EventType.Heartbeat));

        realRegistry.Get("live-agent").Should().NotBeNull();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static AgentManifest MakeManifest(string agentId)
        => new() { AgentId = agentId, Name = agentId, Template = "base" };

    private static MessageEnvelope WrapInEnvelope<T>(T payload, EventType eventType)
        => new()
        {
            SourceId = "test",
            EventType = eventType,
            Payload = JsonSerializer.SerializeToElement(payload),
        };
}
