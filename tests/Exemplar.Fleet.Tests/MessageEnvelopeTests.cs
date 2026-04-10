using System.Text.Json;
using Exemplar.Fleet.Models;
using FluentAssertions;
using Xunit;

namespace Exemplar.Fleet.Tests;

/// <summary>
/// Verifies that MessageEnvelope serialises to / from JSON using snake_case field names
/// so that round-trips with the Python nats-core library are correct.
/// </summary>
public class MessageEnvelopeTests
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        // No naming policy — relies entirely on [JsonPropertyName] attributes.
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void MessageEnvelope_SerializesToSnakeCaseJson()
    {
        var envelope = new MessageEnvelope
        {
            SourceId = "exemplar-api",
            EventType = EventType.Event,
            CorrelationId = "corr-123",
            Payload = JsonSerializer.SerializeToElement(new { customer_id = "abc" }),
        };

        var json = JsonSerializer.Serialize(envelope, _opts);

        json.Should().Contain("\"source_id\"");
        json.Should().Contain("\"event_type\"");
        json.Should().Contain("\"correlation_id\"");
        json.Should().Contain("\"message_id\"");
        json.Should().Contain("\"timestamp\"");
        json.Should().Contain("\"version\"");
        json.Should().Contain("\"payload\"");
    }

    [Fact]
    public void MessageEnvelope_EventType_SerializesToLowercaseString()
    {
        var envelope = new MessageEnvelope
        {
            SourceId = "test",
            EventType = EventType.Register,
            Payload = JsonSerializer.SerializeToElement(new { }),
        };

        var json = JsonSerializer.Serialize(envelope, _opts);
        json.Should().Contain("\"register\"");
    }

    [Fact]
    public void MessageEnvelope_RoundTrips_PreservingAllFields()
    {
        var original = new MessageEnvelope
        {
            SourceId = "exemplar-api",
            EventType = EventType.Command,
            CorrelationId = "corr-456",
            Project = "exemplar",
            Payload = JsonSerializer.SerializeToElement(new { key = "value" }),
        };

        var json = JsonSerializer.Serialize(original, _opts);
        var restored = JsonSerializer.Deserialize<MessageEnvelope>(json, _opts)!;

        restored.SourceId.Should().Be(original.SourceId);
        restored.EventType.Should().Be(original.EventType);
        restored.CorrelationId.Should().Be(original.CorrelationId);
        restored.Project.Should().Be(original.Project);
        restored.Version.Should().Be(original.Version);
    }

    [Theory]
    [InlineData("register", EventType.Register)]
    [InlineData("deregister", EventType.Deregister)]
    [InlineData("heartbeat", EventType.Heartbeat)]
    [InlineData("command", EventType.Command)]
    [InlineData("result", EventType.Result)]
    [InlineData("event", EventType.Event)]
    public void EventType_DeserializesFromPythonWireValue(string wireValue, EventType expected)
    {
        // Simulates a JSON fragment arriving from the Python nats-core side.
        var json = $"{{\"source_id\":\"py-agent\",\"event_type\":\"{wireValue}\",\"payload\":{{}}}}";
        var envelope = JsonSerializer.Deserialize<MessageEnvelope>(json, _opts)!;

        envelope.EventType.Should().Be(expected);
    }

    [Fact]
    public void AgentManifest_SerializesToSnakeCaseJson()
    {
        var manifest = new AgentManifest
        {
            AgentId = "my-agent",
            Name = "My Agent",
            Template = "base",
            Intents = [new IntentCapability { Name = "customer.enrich", Description = "Enrich customers" }],
            Tools = [new ToolCapability { Name = "customer_enrichment" }],
        };

        var json = JsonSerializer.Serialize(manifest, _opts);

        json.Should().Contain("\"agent_id\"");
        json.Should().Contain("\"max_concurrent\"");
        json.Should().Contain("\"trust_tier\"");
    }

    [Fact]
    public void AgentManifest_RoundTrips_PreservingCapabilities()
    {
        var original = new AgentManifest
        {
            AgentId = "arch-agent",
            Name = "Architect Agent",
            Template = "reasoning",
            Version = "1.2.0",
            MaxConcurrent = 3,
            Intents = [new IntentCapability { Name = "architecture.review" }],
            Tools = [new ToolCapability { Name = "architect_feasibility" }],
        };

        var json = JsonSerializer.Serialize(original, _opts);
        var restored = JsonSerializer.Deserialize<AgentManifest>(json, _opts)!;

        restored.AgentId.Should().Be("arch-agent");
        restored.Version.Should().Be("1.2.0");
        restored.MaxConcurrent.Should().Be(3);
        restored.Intents.Should().ContainSingle(i => i.Name == "architecture.review");
        restored.Tools.Should().ContainSingle(t => t.Name == "architect_feasibility");
    }
}
