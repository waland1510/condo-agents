using CondoAgents.Agents.Fakes;
using CondoAgents.Agents.Orchestration;
using CondoAgents.Agents.Tools;
using CondoAgents.Clients.Fakes;
using CondoAgents.Domain.Scoping;
using CondoAgents.Mcp;
using CondoAgents.Tests.Agents;

namespace CondoAgents.Tests.Mcp;

public sealed class ScopedMcpToolsTests
{
    [Fact]
    public void Exposes_the_scoped_tools_and_ask_without_any_scope_argument()
    {
        var toolbox = new ScopedToolbox(new Scope("jardins-plateau"), Fixture.Retrieval, new PermissivePromptShield());
        var orchestrator = new DueDiligenceOrchestrator(new ScriptedChatClient(), Fixture.Retrieval, new RegexPiiDetector(), new PermissivePromptShield(), new RecordingMessenger(), new InMemoryRunStore());

        var tools = ScopedMcpTools.Build(toolbox, orchestrator);

        Assert.Equal(
            ["search_documents", "open_document", "civil_code_lookup", "reserve_fund_projection", "ask_due_diligence"],
            tools.Select(t => t.ProtocolTool.Name));
        foreach (var tool in tools)
        {
            var schema = tool.ProtocolTool.InputSchema;
            var properties = schema.TryGetProperty("properties", out var p) ? p.EnumerateObject().Select(o => o.Name).ToList() : [];
            Assert.DoesNotContain(properties, n => n.Contains("building", StringComparison.OrdinalIgnoreCase) || n.Contains("scope", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Scope_comes_from_the_server_arguments_not_from_the_client()
    {
        Assert.Equal("jardins-plateau", ScopedMcpTools.ScopeFromArgs(["--building", "jardins-plateau"]).BuildingId);
        Assert.Throws<ScopeException>(() => ScopeFilter.Build(ScopedMcpTools.ScopeFromArgs(["--building", "Bad Id"])));
    }
}