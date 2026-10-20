using System.Text.Json;

using CondoAgents.Agents.Runtime;
using CondoAgents.Agents.Tools;
using CondoAgents.Clients;
using CondoAgents.Domain.Retrieval;
using CondoAgents.Domain.Scoping;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CondoAgents.Agents.Orchestration;

/// <summary>Planner → specialists → critic → synthesis. The model decides the plan and the tool calls; the orchestrator decides what runs, what is kept, and when to stop for a human.</summary>
public sealed class DueDiligenceOrchestrator(
    IChatClient model,
    IRetrievalClient retrieval,
    IPiiDetector pii,
    IPromptShield shield,
    IOutboundMessenger messenger,
    IRunStore store)
{
    public const int MaxAttemptsPerStep = 2;

    /// <summary>Receives every trace event as it is recorded; for a console or a stream, not for control flow.</summary>
    public Action<RunEvent>? Observer { get; set; }

    public async Task<AgentRun> StartAsync(Scope scope, string question, RunBudget? budget = null, CancellationToken ct = default)
    {
        var run = await CreateAsync(scope, question, budget, ct);
        return await ContinueAsync(run, ct);
    }

    /// <summary>Persists a run before any work so a caller can hand it to a background task and poll it by id.</summary>
    public async Task<AgentRun> CreateAsync(Scope scope, string question, RunBudget? budget = null, CancellationToken ct = default)
    {
        ScopeFilter.Build(scope);
        var run = new AgentRun { Id = AgentRun.NewId(), Scope = scope, Question = question, Budget = budget ?? RunBudget.Default };
        await Persist(run, ct);
        return run;
    }

    public async Task<AgentRun> ReloadAsync(string runId, CancellationToken ct = default)
        => await store.LoadAsync(runId, ct) ?? throw new KeyNotFoundException($"Run '{runId}' not found.");

    public async Task<AgentRun> DecideAsync(string runId, string approvalId, bool approved, string decidedBy, CancellationToken ct = default)
    {
        var run = await store.LoadAsync(runId, ct) ?? throw new KeyNotFoundException($"Run '{runId}' not found.");
        var index = run.Approvals.FindIndex(a => a.Id == approvalId && a.Approved is null);
        if (index < 0)
        {
            throw new InvalidOperationException($"Run '{runId}' has no pending approval '{approvalId}'.");
        }

        run.Approvals[index] = run.Approvals[index] with { Approved = approved, DecidedBy = decidedBy, DecidedAt = DateTimeOffset.UtcNow };
        run.Status = RunStatus.Running;
        await Persist(run, ct);
        return await ContinueAsync(run, ct);
    }

    public async Task<AgentRun> ContinueAsync(AgentRun run, CancellationToken ct = default)
    {
        var trace = new RunTrace(Observer);
        var baseline = run.Events.Count;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(run.Budget.Duration);
        using var activity = Telemetry.Source.StartActivity("run");
        activity?.SetTag("run.id", run.Id);

        try
        {
            if (run.Plan is null)
            {
                var refusal = await new Gates(pii, shield, trace).CheckQuestionAsync(run.Question, timeout.Token);
                if (refusal is not null)
                {
                    run.Status = RunStatus.Refused;
                    run.Final = new FinalAnswer(refusal.Reason, [], []);
                    return await Finish(run, trace, baseline, ct);
                }

                run.Plan = await PlanAsync(run, trace, timeout.Token);
                run.Status = RunStatus.Running;
                foreach (var step in run.Plan.InExecutionOrder())
                {
                    run.Steps.Add(new StepRecord { StepId = step.Id, Specialist = step.Specialist, Question = step.Question });
                }

                await Persist(run, trace, baseline, ct);
            }

            foreach (var step in run.Plan.InExecutionOrder())
            {
                var record = run.Steps.Single(s => s.StepId == step.Id);
                if (record.Completed)
                {
                    continue;
                }

                var paused = await RunStepAsync(run, step, record, trace, timeout.Token);
                await Persist(run, trace, baseline, ct);
                if (paused)
                {
                    return run;
                }
            }

            run.Final = Synthesize(run);
            run.Status = run.Approvals.Any(a => a.Approved == false) ? RunStatus.Rejected : RunStatus.Completed;
            return await Finish(run, trace, baseline, ct);
        }
        catch (Exception ex) when (ex is BudgetExceededException or InvalidPlanException or OperationCanceledException or HttpRequestException or InvalidOperationException)
        {
            run.Status = RunStatus.Failed;
            run.Error = ex.Message;
            return await Finish(run, trace, baseline, ct);
        }
    }

    private async Task<Plan> PlanAsync(AgentRun run, RunTrace trace, CancellationToken ct)
    {
        var planner = AgentFactory.Create(model, "planner", Prompts.Planner, [], run.Budget, trace);
        var session = await planner.CreateSessionAsync(ct);
        var response = await planner.RunCheckedAsync<Plan>(run.Question, session, trace, ct);
        var plan = response.Result with { Steps = (response.Result.Steps ?? []).Select(st => st with { DependsOn = st.DependsOn ?? [] }).ToList() };
        plan.Validate();
        return plan;
    }

    /// <summary>Returns true when the step stopped for a human; the run is persisted with the serialized session so any process can resume it.</summary>
    private async Task<bool> RunStepAsync(AgentRun run, PlanStep step, StepRecord record, RunTrace trace, CancellationToken ct)
    {
        var toolbox = new ScopedToolbox(run.Scope, retrieval, shield, messenger);
        var tools = step.Specialist switch
        {
            Specialists.Finance => toolbox.FinanceOnly(),
            Specialists.Legal => toolbox.ReferenceOnly(),
            _ => step.ContactSyndicate ? [.. toolbox.All(), .. toolbox.Irreversible()] : toolbox.All(),
        };
        var agent = AgentFactory.Create(model, step.Specialist, Prompts.Specialist(step.Specialist), tools, run.Budget, trace);

        AgentSession session;
        IEnumerable<ChatMessage> input;
        var pending = run.Approvals.Where(a => a.StepId == step.Id && a.Approved is not null && !a.ArgumentsJson.StartsWith("consumed:", StringComparison.Ordinal)).ToList();
        if (record.PausedSession is { } saved && pending.Count > 0)
        {
            session = await agent.DeserializeSessionAsync(saved, cancellationToken: ct);
            input = pending.Select(a => new ChatMessage(ChatRole.User, [ApprovalResponse(a)]));
            foreach (var a in pending)
            {
                run.Approvals[run.Approvals.IndexOf(a)] = a with { ArgumentsJson = "consumed:" + a.ArgumentsJson };
            }

            record.PausedSession = null;
        }
        else
        {
            session = await agent.CreateSessionAsync(ct);
            input = [new ChatMessage(ChatRole.User, StepPrompt(run, step, record))];
        }

        record.Attempts++;
        var response = await agent.RunCheckedAsync(input, session, trace, ct);

        var approvals = response.Messages.SelectMany(m => m.Contents.OfType<ToolApprovalRequestContent>()).ToList();
        if (approvals.Count > 0)
        {
            foreach (var request in approvals)
            {
                var call = (FunctionCallContent)request.ToolCall;
                run.Approvals.Add(new PendingApproval(
                    request.RequestId, step.Id, call.Name, JsonSerializer.Serialize(call.Arguments, RunJson.Options)));
                trace.Record(RunTrace.Gate, "approval", DateTimeOffset.UtcNow, 0, detail: $"{call.Name} awaiting approval");
            }

            record.PausedSession = await agent.SerializeSessionAsync(session, cancellationToken: ct);
            run.Status = RunStatus.AwaitingApproval;
            return true;
        }

        var answer = ParseAnswer(response.Text);
        var evidence = RetrievedChunk.InEvidenceOrder(toolbox.Evidence);
        var citations = CitationValidator.Resolve(answer.Evidence, evidence);
        record.Answer = answer;
        record.Citations = citations.Citations;
        record.RejectedLabels = citations.RejectedLabels;
        record.Evidence = evidence;

        if (answer.Kind == AgentAnswer.KindAction)
        {
            record.Completed = true;
            return false;
        }

        var verdict = await CritiqueAsync(run, answer, evidence, trace, ct);
        record.Critic = verdict;
        if (!verdict.Pass && record.Attempts < MaxAttemptsPerStep)
        {
            record.PausedSession = null;
            return await RunStepAsync(run, step, record, trace, ct);
        }

        record.KeptClaims = Claims.Split(answer).Where(c => c.Labels.Count > 0 && !verdict.Unsupported!.Any(u => u.Claim == c.Text)).ToList();
        record.Completed = true;
        return false;
    }

    private static ToolApprovalResponseContent ApprovalResponse(PendingApproval approval)
    {
        var call = new FunctionCallContent(approval.Id, approval.Tool, JsonSerializer.Deserialize<Dictionary<string, object?>>(approval.ArgumentsJson, RunJson.Options));
        return new ToolApprovalResponseContent(approval.Id, approval.Approved == true, call) { Reason = approval.Approved == true ? null : $"Rejected by {approval.DecidedBy}." };
    }

    private async Task<CriticVerdict> CritiqueAsync(AgentRun run, AgentAnswer answer, IReadOnlyList<RetrievedChunk> evidence, RunTrace trace, CancellationToken ct)
    {
        var claims = Claims.Split(answer);
        if (claims.Count == 0)
        {
            return new CriticVerdict(true, [], []);
        }

        var critic = AgentFactory.Create(model, "critic", Prompts.Critic, [], run.Budget, trace);
        var session = await critic.CreateSessionAsync(ct);
        var payload = JsonSerializer.Serialize(new
        {
            claims = claims.Select(c => new { text = c.Text, labels = c.Labels }),
            evidence = evidence.Select((e, i) => new { label = RetrievedChunk.EvidenceLabel(i), e.DocumentId, e.Text }),
        }, RunJson.Options);
        var response = await critic.RunCheckedAsync<CriticVerdict>(payload, session, trace, ct);
        var verdict = response.Result.Normalized();
        return verdict.Unsupported!.Count == 0 ? verdict with { Pass = true } : verdict;
    }

    private static string StepPrompt(AgentRun run, PlanStep step, StepRecord record)
    {
        var context = step.DependsOn
            .Select(id => run.Steps.Single(s => s.StepId == id))
            .Where(s => s.Answer is not null)
            .Select(s => $"[{s.Specialist}] {s.Answer!.Text}");
        var parts = new List<string> { $"Buyer's question: {run.Question}", $"Your task: {step.Question}" };
        parts.AddRange(context.Select(c => "Earlier finding: " + c));
        if (record.Critic is { Pass: false } critic)
        {
            parts.Add("A reviewer rejected these claims from your previous attempt; fix or drop them: " +
                      string.Join(" | ", (critic.Unsupported ?? []).Select(u => $"{u.Claim} ({u.Reason})")));
        }

        return string.Join("\n", parts);
    }

    private static AgentAnswer ParseAnswer(string text)
    {
        try
        {
            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            var json = start >= 0 && end > start ? text[start..(end + 1)] : text;
            var parsed = JsonSerializer.Deserialize<AgentAnswer>(json, RunJson.Options);
            return parsed is null
                ? new AgentAnswer(AgentAnswer.KindClarification, text, [])
                : parsed with { Kind = parsed.Kind ?? AgentAnswer.KindAnswer, Text = parsed.Text ?? "", Evidence = parsed.Evidence ?? [] };
        }
        catch (JsonException)
        {
            return new AgentAnswer(AgentAnswer.KindClarification, text, []);
        }
    }

    private static FinalAnswer Synthesize(AgentRun run)
    {
        var sections = new List<string>();
        var citations = new List<Citation>();
        var dropped = new List<UnsupportedClaim>();
        foreach (var step in run.Steps)
        {
            if (step.KeptClaims.Count > 0)
            {
                sections.Add($"{step.Specialist}: {string.Join(" ", step.KeptClaims.Select(c => c.Text))}");
                citations.AddRange(step.Citations.Where(c => step.KeptClaims.Any(k => k.Labels.Contains(c.Label))).Select(c => c with { Label = $"{step.StepId}/{c.Label}" }));
            }
            else if (step.Answer is not null)
            {
                sections.Add($"{step.Specialist}: {(step.Answer.Kind == AgentAnswer.KindAnswer ? "no claim survived review." : step.Answer.Text)}");
            }
            else if (!step.Completed)
            {
                sections.Add($"{step.Specialist}: waiting for approval.");
            }

            dropped.AddRange(step.Critic?.Unsupported ?? []);
        }

        return new FinalAnswer(string.Join("\n", sections), citations, dropped);
    }

    private async Task<AgentRun> Finish(AgentRun run, RunTrace trace, int baseline, CancellationToken ct)
    {
        await Persist(run, trace, baseline, ct);
        return run;
    }

    /// <summary>Events from earlier processes stay; this continuation's trace is re-appended in full after them.</summary>
    private async Task Persist(AgentRun run, RunTrace trace, int baseline, CancellationToken ct)
    {
        run.Events.RemoveRange(baseline, run.Events.Count - baseline);
        run.Events.AddRange(trace.Events.Select(e => e with { Sequence = baseline + e.Sequence }));
        await Persist(run, ct);
    }

    private async Task Persist(AgentRun run, CancellationToken ct)
    {
        run.UpdatedAt = DateTimeOffset.UtcNow;
        await store.SaveAsync(run, ct);
    }
}