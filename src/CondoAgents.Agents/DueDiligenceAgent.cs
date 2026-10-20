using CondoAgents.Agents.Runtime;
using CondoAgents.Agents.Tools;
using CondoAgents.Clients;
using CondoAgents.Domain.Retrieval;
using CondoAgents.Domain.Scoping;

using Microsoft.Extensions.AI;

namespace CondoAgents.Agents;

/// <summary>The single-agent tier: gates, one tool-using agent, citation validation. The planner and critic build on the same pieces.</summary>
public sealed class DueDiligenceAgent(IChatClient model, IRetrievalClient retrieval, IPiiDetector pii, IPromptShield shield)
{
    public const string Name = "due-diligence";

    public const string Instructions = """
        You help a prospective buyer understand a condominium from its own documents.
        Use search_documents before answering anything factual. Cite evidence by its label (S1, S2, ...).
        Quote amounts and dates as printed; never compute them yourself, call reserve_fund_projection instead.
        A proposal that was rejected or deferred is not a decision; say so explicitly.
        Prefer the most recent version of a bylaw and name the version you relied on.
        If the documents do not answer the question, say that instead of guessing.
        Respond with JSON: {"kind":"answer|refusal|clarification","text":"...","evidence":["S1","S2"]}.
        """;

    public async Task<AnswerResult> AskAsync(Scope scope, string question, RunBudget? budget = null, CancellationToken ct = default)
    {
        budget ??= RunBudget.Default;
        var trace = new RunTrace();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(budget.Duration);

        var refusal = await new Gates(pii, shield, trace).CheckQuestionAsync(question, timeout.Token);
        if (refusal is not null)
        {
            return new AnswerResult(new AgentAnswer(AgentAnswer.KindRefusal, refusal.Reason, []), [], [], [], trace);
        }

        var toolbox = new ScopedToolbox(scope, retrieval, shield);
        var agent = AgentFactory.Create(model, Name, Instructions, toolbox.All(), budget, trace);
        var session = await agent.CreateSessionAsync(timeout.Token);
        var response = await agent.RunCheckedAsync<AgentAnswer>(question, session, trace, timeout.Token);

        var answer = response.Result;
        var evidence = RetrievedChunk.InEvidenceOrder(toolbox.Evidence);
        var citations = CitationValidator.Resolve(answer.Evidence, evidence);
        return new AnswerResult(answer, citations.Citations, citations.RejectedLabels, evidence, trace);
    }
}