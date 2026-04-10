using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Images;
using Exemplar.Fleet.Models;
using Exemplar.Fleet.Registry;
using Exemplar.Fleet.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.Serializers.Json;
using Xunit;

namespace Exemplar.Fleet.Tests;

/// <summary>
/// Integration tests against a real NATS server spun up via Testcontainers.
/// Tests the full fleet discovery → request-reply cycle.
///
/// Prerequisites: Docker must be running.
/// </summary>
[Collection("NatsIntegration")]
public sealed class NatsIntegrationTests : IAsyncLifetime
{
    private readonly DotNet.Testcontainers.Containers.IContainer _natsContainer;
    private NatsConnection? _conn;
    private string? _natsUrl;

    public NatsIntegrationTests()
    {
        _natsContainer = new ContainerBuilder(new DockerImage("nats:latest"))
            .WithCommand("-js")
            .WithPortBinding(4222, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(4222))
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _natsContainer.StartAsync();
        var port = _natsContainer.GetMappedPublicPort(4222);
        _natsUrl = $"nats://localhost:{port}";

        var opts = NatsOpts.Default with
        {
            Url = _natsUrl,
            SerializerRegistry = NatsJsonSerializerRegistry.Default,
        };
        _conn = new NatsConnection(opts);
        await _conn.ConnectAsync();
    }

    public async Task DisposeAsync()
    {
        if (_conn is not null)
            await _conn.DisposeAsync();
        await _natsContainer.StopAsync();
        await _natsContainer.DisposeAsync();
    }

    // ── Fleet discovery: register → find ──────────────────────────────────────

    [Fact]
    public async Task FleetDiscovery_WhenAgentRegisters_IsAvailableInRegistry()
    {
        var registry = new InMemoryManifestRegistry();
        var opts = Options.Create(new FleetOptions { NatsUrl = _natsUrl! });

        var service = new FleetDiscoveryService(
            _conn!, registry, opts, NullLogger<FleetDiscoveryService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var _ = service.StartAsync(cts.Token);

        // Give the subscription a moment to initialise.
        await Task.Delay(200, cts.Token);

        var manifest = new AgentManifest
        {
            AgentId = "test-agent",
            Name = "Test Agent",
            Template = "base",
            Intents = [new IntentCapability { Name = "test.intent" }],
            Tools = [new ToolCapability { Name = "test_tool" }],
        };
        var envelope = new MessageEnvelope
        {
            SourceId = "test-agent",
            EventType = EventType.Register,
            Payload = JsonSerializer.SerializeToElement(manifest),
        };

        await _conn!.PublishAsync("fleet.register", envelope, cancellationToken: cts.Token);

        // Allow message to be processed.
        await Task.Delay(300, cts.Token);

        await service.StopAsync(CancellationToken.None);

        registry.Get("test-agent").Should().NotBeNull();
        registry.FindByTool("test_tool").Should().ContainSingle();
        registry.FindByIntent("test.*").Should().ContainSingle();
    }

    // ── Fleet discovery: deregister ────────────────────────────────────────────

    [Fact]
    public async Task FleetDiscovery_WhenAgentDeregisters_IsRemovedFromRegistry()
    {
        var registry = new InMemoryManifestRegistry();
        var opts = Options.Create(new FleetOptions { NatsUrl = _natsUrl! });
        var service = new FleetDiscoveryService(
            _conn!, registry, opts, NullLogger<FleetDiscoveryService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var _ = service.StartAsync(cts.Token);
        await Task.Delay(200, cts.Token);

        // Register an agent first.
        var manifest = new AgentManifest { AgentId = "leaving-agent", Name = "Leaving", Template = "base" };
        await _conn!.PublishAsync("fleet.register",
            new MessageEnvelope
            {
                SourceId = "leaving-agent",
                EventType = EventType.Register,
                Payload = JsonSerializer.SerializeToElement(manifest),
            },
            cancellationToken: cts.Token);

        await Task.Delay(200, cts.Token);
        registry.Get("leaving-agent").Should().NotBeNull("agent should be registered first");

        // Now deregister it.
        await _conn.PublishAsync("fleet.deregister",
            new MessageEnvelope
            {
                SourceId = "leaving-agent",
                EventType = EventType.Deregister,
                Payload = JsonSerializer.SerializeToElement(
                    new AgentDeregistrationPayload { AgentId = "leaving-agent", Reason = "shutdown" }),
            },
            cancellationToken: cts.Token);

        await Task.Delay(300, cts.Token);
        await service.StopAsync(CancellationToken.None);

        registry.Get("leaving-agent").Should().BeNull();
    }

    // ── Heartbeat ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task FleetDiscovery_HeartbeatReceived_AgentRemainsInRegistry()
    {
        var registry = new InMemoryManifestRegistry();
        var opts = Options.Create(new FleetOptions { NatsUrl = _natsUrl! });
        var service = new FleetDiscoveryService(
            _conn!, registry, opts, NullLogger<FleetDiscoveryService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var _ = service.StartAsync(cts.Token);
        await Task.Delay(200, cts.Token);

        var manifest = new AgentManifest { AgentId = "heartbeat-agent", Name = "HB", Template = "base" };
        await _conn!.PublishAsync("fleet.register",
            new MessageEnvelope
            {
                SourceId = "heartbeat-agent",
                EventType = EventType.Register,
                Payload = JsonSerializer.SerializeToElement(manifest),
            },
            cancellationToken: cts.Token);

        await Task.Delay(200, cts.Token);

        // Publish a heartbeat.
        await _conn.PublishAsync("fleet.heartbeat.heartbeat-agent",
            new MessageEnvelope
            {
                SourceId = "heartbeat-agent",
                EventType = EventType.Heartbeat,
                Payload = JsonSerializer.SerializeToElement(
                    new AgentHeartbeatPayload { AgentId = "heartbeat-agent" }),
            },
            cancellationToken: cts.Token);

        await Task.Delay(200, cts.Token);
        await service.StopAsync(CancellationToken.None);

        registry.Get("heartbeat-agent").Should().NotBeNull();
    }
}
