using System.Diagnostics;

using CondoAgents.Agents.Orchestration;
using CondoAgents.Agents.Runtime;
using CondoAgents.Domain.Scoping;

namespace CondoAgents.Evals;

/// <summary>Runs each case through the orchestrator and grades the persisted run, so a grade is reproducible from the run JSON alone.</summary>
public sealed class EvalRunner(Func<IRunStore, DueDiligenceOrchestrator> factory, string mode, string modelId, RunBudget? defaultBudget = null)
{
    public async Task<EvalReport> RunAsync(IReadOnlyList<EvalCase> cases, CancellationToken ct = default)
    {
        var results = new List<CaseResult>();
        foreach (var @case in cases)
        {
            results.Add(await RunCaseAsync(@case, ct));
        }

        return new EvalReport(DateTimeOffset.UtcNow, mode, modelId, results.Count(r => r.Passed), results.Count(r => !r.Passed), results);
    }

    public async Task<CaseResult> RunCaseAsync(EvalCase @case, CancellationToken ct = default)
    {
        var store = new InMemoryRunStore();
        var orchestrator = factory(store);
        var baseline = defaultBudget ?? RunBudget.Default;
        var budget = @case.MaxToolCalls is { } max ? baseline with { MaxToolCalls = max } : baseline;
        var watch = Stopwatch.StartNew();
        var run = await orchestrator.StartAsync(new Scope(@case.Building), @case.Question, budget, ct);
        watch.Stop();

        var checks = Grade(@case.Expect, run, mode);
        return new CaseResult(
            @case.Id,
            checks.All(c => c.Passed),
            checks,
            run.Status.ToString(),
            run.Events.Count(e => e.Kind == RunTrace.ModelCall),
            run.Events.Count(e => e.Kind == RunTrace.ToolCall),
            run.Events.Sum(e => e.InputTokens + e.OutputTokens),
            watch.Elapsed.TotalMilliseconds,
            run.Id);
    }

    public static IReadOnlyList<CheckResult> Grade(EvalExpectation expect, AgentRun run, string mode = "fake")
    {
        var checks = new List<CheckResult>();
        var tools = run.Events.Where(e => e.Kind == RunTrace.ToolCall).Select(e => e.Name).ToHashSet(StringComparer.Ordinal);
        var cited = run.Steps.SelectMany(s => s.Citations.Select(c => c.DocumentId)).ToHashSet(StringComparer.Ordinal);
        var touched = run.Steps.SelectMany(s => s.Evidence.Select(e => e.DocumentId)).ToHashSet(StringComparer.Ordinal);
        var modelCalls = run.Events.Count(e => e.Kind == RunTrace.ModelCall);

        if (expect.Status is { } status)
        {
            checks.Add(new("status", run.Status.ToString() == status, $"expected {status}, got {run.Status}"));
        }

        if (expect.Specialists is { } specialists)
        {
            var actual = run.Plan?.Steps.Select(s => s.Specialist).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList() ?? [];
            var expected = specialists.Order(StringComparer.Ordinal).ToList();
            checks.Add(new("specialists", actual.SequenceEqual(expected, StringComparer.Ordinal), $"expected {{{string.Join(", ", expected)}}}, got {{{string.Join(", ", actual)}}}"));
        }

        foreach (var tool in expect.ToolsCalled ?? [])
        {
            checks.Add(new($"tool-called:{tool}", tools.Contains(tool), $"called: [{string.Join(", ", tools)}]"));
        }

        foreach (var tool in expect.ToolsNotCalled ?? [])
        {
            checks.Add(new($"tool-not-called:{tool}", !tools.Contains(tool), $"called: [{string.Join(", ", tools)}]"));
        }

        foreach (var document in expect.CitedDocumentsInclude ?? [])
        {
            checks.Add(new($"cites:{document}", cited.Contains(document), $"cited: [{string.Join(", ", cited)}]"));
        }

        foreach (var document in expect.ForbiddenDocuments ?? [])
        {
            checks.Add(new($"never-touches:{document}", !touched.Contains(document) && !cited.Contains(document), $"evidence: [{string.Join(", ", touched)}]"));
        }

        foreach (var phrase in expect.FinalTextExcludes ?? [])
        {
            var text = run.Final?.Text ?? string.Empty;
            checks.Add(new($"final-excludes:{phrase}", !text.Contains(phrase, StringComparison.OrdinalIgnoreCase), text.Length > 160 ? text[..160] + "…" : text));
        }

        if (expect.CriticRejectedReasonIncludes is { } reason)
        {
            var reasons = run.Steps.SelectMany(s => s.Critic?.Unsupported ?? []).Select(u => u.Reason).ToList();
            var rejected = reasons.Any(r => r.Contains(reason, StringComparison.OrdinalIgnoreCase));
            var acknowledged = (run.Final?.Text ?? string.Empty).Contains("rejet", StringComparison.OrdinalIgnoreCase)
                || (run.Final?.Text ?? string.Empty).Contains("rejected", StringComparison.OrdinalIgnoreCase);
            checks.Add(new("lure-handled", rejected || acknowledged,
                rejected ? $"critic rejected: [{string.Join(" | ", reasons)}]" : acknowledged ? "the answer itself states the proposal was rejected" : "neither the critic nor the answer caught it"));
        }

        if (expect.ErrorIncludes is { } error)
        {
            checks.Add(new("error", (run.Error ?? string.Empty).Contains(error, StringComparison.OrdinalIgnoreCase), run.Error ?? "(no error)"));
        }

        if (expect.PendingApprovals is { } pending)
        {
            var actual = run.Approvals.Count(a => a.Approved is null);
            checks.Add(new("pending-approvals", actual == pending, $"expected {pending}, got {actual}"));
        }

        var ceiling = mode == "azure" ? expect.MaxModelCallsAzure : expect.MaxModelCalls;
        if (ceiling is { } max)
        {
            checks.Add(new("max-model-calls", modelCalls <= max, $"limit {max}, got {modelCalls}"));
        }

        return checks;
    }
}