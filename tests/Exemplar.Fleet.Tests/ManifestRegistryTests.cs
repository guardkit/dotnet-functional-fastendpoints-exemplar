using Exemplar.Fleet.Models;
using Exemplar.Fleet.Registry;
using FluentAssertions;
using Xunit;

namespace Exemplar.Fleet.Tests;

public class ManifestRegistryTests
{
    private readonly InMemoryManifestRegistry _sut = new();

    // ── Register ───────────────────────────────────────────────────────────────

    [Fact]
    public void Register_AddsManifestToRegistry()
    {
        var manifest = MakeManifest("architect-agent");
        _sut.Register(manifest);

        _sut.Get("architect-agent").Should().Be(manifest);
    }

    [Fact]
    public void Register_ReplacesExistingManifestForSameAgentId()
    {
        var original = MakeManifest("agent-x");
        var updated = original with { Version = "1.0.0" };

        _sut.Register(original);
        _sut.Register(updated);

        _sut.Get("agent-x")!.Version.Should().Be("1.0.0");
    }

    // ── Deregister ─────────────────────────────────────────────────────────────

    [Fact]
    public void Deregister_RemovesManifestFromRegistry()
    {
        _sut.Register(MakeManifest("temp-agent"));
        _sut.Deregister("temp-agent");

        _sut.Get("temp-agent").Should().BeNull();
    }

    [Fact]
    public void Deregister_IsNoOpWhenAgentNotRegistered()
    {
        var act = () => _sut.Deregister("ghost-agent");
        act.Should().NotThrow();
    }

    // ── Get ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Get_ReturnsNullWhenAgentNotRegistered()
    {
        _sut.Get("missing").Should().BeNull();
    }

    // ── All ────────────────────────────────────────────────────────────────────

    [Fact]
    public void All_ReturnsAllRegisteredManifests()
    {
        _sut.Register(MakeManifest("a1"));
        _sut.Register(MakeManifest("a2"));

        _sut.All().Should().HaveCount(2);
    }

    // ── FindByTool ─────────────────────────────────────────────────────────────

    [Fact]
    public void FindByTool_ReturnsAgentsThatDeclareTool()
    {
        var manifest = MakeManifest("lpa-agent",
            tools: [new ToolCapability { Name = "lpa_instruction_parse" }]);
        _sut.Register(manifest);

        var results = _sut.FindByTool("lpa_instruction_parse");

        results.Should().ContainSingle().Which.AgentId.Should().Be("lpa-agent");
    }

    [Fact]
    public void FindByTool_IsEmptyWhenNoAgentDeclaresTool()
    {
        _sut.Register(MakeManifest("agent-a",
            tools: [new ToolCapability { Name = "other_tool" }]));

        _sut.FindByTool("missing_tool").Should().BeEmpty();
    }

    [Fact]
    public void FindByTool_MatchesToolNameCaseInsensitively()
    {
        _sut.Register(MakeManifest("agent-b",
            tools: [new ToolCapability { Name = "MyTool" }]));

        _sut.FindByTool("mytool").Should().ContainSingle();
    }

    // ── FindByIntent ───────────────────────────────────────────────────────────

    [Fact]
    public void FindByIntent_ExactMatch_ReturnsAgent()
    {
        var manifest = MakeManifest("product-owner",
            intents: [new IntentCapability { Name = "product.prioritise" }]);
        _sut.Register(manifest);

        _sut.FindByIntent("product.prioritise").Should().ContainSingle();
    }

    [Fact]
    public void FindByIntent_GlobWildcard_MatchesAllIntentsInNamespace()
    {
        _sut.Register(MakeManifest("po-agent", intents:
        [
            new IntentCapability { Name = "product.prioritise" },
            new IntentCapability { Name = "product.groom" },
        ]));
        _sut.Register(MakeManifest("arch-agent", intents:
        [
            new IntentCapability { Name = "architecture.review" },
        ]));

        var results = _sut.FindByIntent("product.*");

        results.Should().ContainSingle().Which.AgentId.Should().Be("po-agent");
    }

    [Fact]
    public void FindByIntent_WildcardStar_MatchesAllAgents()
    {
        _sut.Register(MakeManifest("a", intents: [new IntentCapability { Name = "x.y" }]));
        _sut.Register(MakeManifest("b", intents: [new IntentCapability { Name = "z.w" }]));

        _sut.FindByIntent("*").Should().HaveCount(2);
    }

    [Fact]
    public void FindByIntent_IsEmptyWhenNoMatchingIntent()
    {
        _sut.Register(MakeManifest("agent",
            intents: [new IntentCapability { Name = "customer.enrich" }]));

        _sut.FindByIntent("finance.*").Should().BeEmpty();
    }

    // ── Thread safety sanity check ─────────────────────────────────────────────

    [Fact]
    public async Task Register_Concurrent_DoesNotThrow()
    {
        var tasks = Enumerable.Range(0, 100)
            .Select(i => Task.Run(() => _sut.Register(MakeManifest($"agent-{i}"))));

        var act = async () => await Task.WhenAll(tasks);
        await act.Should().NotThrowAsync();
        _sut.All().Should().HaveCount(100);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static AgentManifest MakeManifest(
        string agentId,
        IReadOnlyList<IntentCapability>? intents = null,
        IReadOnlyList<ToolCapability>? tools = null)
        => new()
        {
            AgentId = agentId,
            Name = agentId,
            Template = "base",
            Intents = intents ?? [],
            Tools = tools ?? [],
        };
}
