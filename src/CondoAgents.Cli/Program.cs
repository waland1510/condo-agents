using System.Globalization;

using CondoAgents.Agents.Fakes;
using CondoAgents.Agents.Orchestration;
using CondoAgents.Agents.Runtime;
using CondoAgents.Clients.Fakes;
using CondoAgents.Corpus;
using CondoAgents.Domain.Scoping;
using CondoAgents.Ingest;

using Microsoft.Extensions.Logging.Abstractions;

if (args.Length < 2 || args[0] is not ("ask" or "approve" or "reject"))
{
    Console.Error.WriteLine("usage: condo ask <building-id> \"<question>\"");
    Console.Error.WriteLine("       condo approve|reject <run-id> <approval-id>");
    return 2;
}

var repoRoot = Path.GetDirectoryName(CorpusLoader.FindDataRoot(AppContext.BaseDirectory))
    ?? throw new InvalidOperationException("data/manifest.json not found.");
var dataRoot = Path.Combine(repoRoot, "data");

var index = new InMemoryIndex();
var pipeline = new IngestPipeline(new PlainTextExtractor(), new IdentityTranslator(), new RegexPiiDetector(), new KeywordRoleAdjudicator(), index, NullLogger<IngestPipeline>.Instance);
await CorpusIngestor.IngestAllAsync(dataRoot, pipeline, index);

var started = DateTimeOffset.UtcNow;
var orchestrator = new DueDiligenceOrchestrator(
    new ScriptedChatClient(), new InMemoryRetrievalClient(index), new RegexPiiDetector(), new PermissivePromptShield(), new RecordingMessenger(),
    new FileRunStore(Path.Combine(repoRoot, "runs")))
{
    Observer = e => Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
        $"  {(e.At - started).TotalMilliseconds,7:F0} ms  {e.Kind,-5} {e.Name,-24} {(e.InputTokens + e.OutputTokens > 0 ? $"{e.InputTokens + e.OutputTokens} tok" : ""),-9} {e.Detail}")),
};

AgentRun run;
if (args[0] == "ask")
{
    Console.WriteLine($"run: {args[1]} · \"{args[2]}\"");
    run = await orchestrator.StartAsync(new Scope(args[1]), args[2]);
}
else
{
    run = await orchestrator.DecideAsync(args[1], args[2], args[0] == "approve", Environment.UserName);
}

Console.WriteLine();
Console.WriteLine($"{run.Id} · {run.Status}");
if (run.Plan is { } plan)
{
    foreach (var step in plan.Steps)
    {
        var record = run.Steps.Single(s => s.StepId == step.Id);
        Console.WriteLine($"  plan {step.Id,-10} {step.Specialist,-10} attempts={record.Attempts} critic={(record.Critic is null ? "-" : record.Critic.Pass ? "pass" : "sent back")}");
    }
}

foreach (var approval in run.Approvals.Where(a => a.Approved is null))
{
    Console.WriteLine($"  awaiting approval {approval.Id}: {approval.Tool} {approval.ArgumentsJson}");
    Console.WriteLine($"  → condo approve {run.Id} {approval.Id}");
}

if (run.Final is { } final)
{
    Console.WriteLine();
    Console.WriteLine(final.Text);
    foreach (var citation in final.Citations)
    {
        Console.WriteLine($"  [{citation.Label}] {citation.Title}{(citation.Page is { } p ? $", p. {p}" : "")}");
    }

    foreach (var dropped in final.Dropped)
    {
        Console.WriteLine($"  dropped: {dropped.Claim} ({dropped.Reason})");
    }
}

if (run.Error is { } error)
{
    Console.WriteLine($"  error: {error}");
}

return run.Status is RunStatus.Failed ? 1 : 0;