using CondoAgents.Agents;
using CondoAgents.Agents.Fakes;
using CondoAgents.Agents.Runtime;
using CondoAgents.Clients.Fakes;
using CondoAgents.Domain.Scoping;

using Microsoft.Extensions.AI;

namespace CondoAgents.Tests.Agents;

public sealed class DueDiligenceAgentTests
{
    private static readonly Scope Scope = new("jardins-plateau");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DueDiligenceAgent Agent(ScriptedChatClient.Script? script = null)
        => new(new ScriptedChatClient(script), Fixture.Retrieval, new RegexPiiDetector(), new PermissivePromptShield());

    [Fact]
    public async Task Answers_with_verified_citations_and_a_trace()
    {
        var result = await Agent().AskAsync(Scope, "Quelle cotisation spéciale a été adoptée pour la toiture?", ct: Ct);

        Assert.Equal(AgentAnswer.KindAnswer, result.Answer.Kind);
        Assert.NotEmpty(result.Citations);
        Assert.Empty(result.RejectedLabels);
        Assert.Contains(result.Citations, c => c.DocumentId == "agm-minutes-2023");
        Assert.Equal(["gate", "tool", "model", "model"], result.Trace.Events.Select(e => e.Kind).OrderBy(k => k == "gate" ? 0 : k == "tool" ? 1 : 2));
        Assert.True(result.Trace.TotalTokens > 0);
    }

    [Fact]
    public async Task A_fabricated_label_never_becomes_a_citation()
    {
        var script = new ScriptedChatClient.Script(FinalJson: (_, _) => """{"kind":"answer","text":"The roof costs 1 $. [S9]","evidence":["S1","S9"]}""");

        var result = await Agent(script).AskAsync(Scope, "roof cost toiture", ct: Ct);

        Assert.Equal(["S9"], result.RejectedLabels);
        Assert.All(result.Citations, c => Assert.Equal("S1", c.Label));
    }

    [Fact]
    public async Task Personal_data_in_the_question_is_refused_before_any_model_call()
    {
        var result = await Agent().AskAsync(Scope, "Is Mme Lucie Pham still suing the syndicate?", ct: Ct);

        Assert.Equal(AgentAnswer.KindRefusal, result.Answer.Kind);
        Assert.Equal(0, result.Trace.ModelCalls);
        Assert.Equal(0, result.Trace.ToolCalls);
    }

    [Fact]
    public async Task Tool_call_budget_stops_a_looping_model()
    {
        var script = new ScriptedChatClient.Script(
            FirstCall: (_, tools) => new FunctionCallContent("c", "search_documents", new Dictionary<string, object?> { ["query"] = "toiture" }),
            FinalJson: (_, _) => throw new InvalidOperationException("never reached: the model keeps calling tools"));
        var looping = new LoopingChatClient(new ScriptedChatClient(script));
        var agent = new DueDiligenceAgent(looping, Fixture.Retrieval, new RegexPiiDetector(), new PermissivePromptShield());

        var ex = await Assert.ThrowsAsync<BudgetExceededException>(() => agent.AskAsync(Scope, "toiture", new RunBudget(MaxModelCalls: 50, MaxToolCalls: 3), Ct));

        Assert.Equal("tool calls", ex.What);
    }

    [Fact]
    public async Task Model_call_budget_is_enforced_under_the_function_loop()
    {
        var script = new ScriptedChatClient.Script(
            FirstCall: (_, _) => new FunctionCallContent("c", "search_documents", new Dictionary<string, object?> { ["query"] = "toiture" }));
        var looping = new LoopingChatClient(new ScriptedChatClient(script));
        var agent = new DueDiligenceAgent(looping, Fixture.Retrieval, new RegexPiiDetector(), new PermissivePromptShield());

        var ex = await Assert.ThrowsAsync<BudgetExceededException>(() => agent.AskAsync(Scope, "toiture", new RunBudget(MaxModelCalls: 2, MaxToolCalls: 100), Ct));

        Assert.Equal("model calls", ex.What);
    }

    /// <summary>Always asks for another tool call, never answers: what a runaway agent looks like.</summary>
    private sealed class LoopingChatClient(IChatClient inner) : DelegatingChatClient(inner)
    {
        private int _n;

        public override Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var call = new FunctionCallContent($"loop-{++_n}", "search_documents", new Dictionary<string, object?> { ["query"] = "toiture" });
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, [call]))
            {
                FinishReason = ChatFinishReason.ToolCalls,
                Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5, TotalTokenCount = 15 },
            });
        }
    }
}