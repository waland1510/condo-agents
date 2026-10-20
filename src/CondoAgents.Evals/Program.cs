using CondoAgents.Agents.Fakes;
using CondoAgents.Agents.Orchestration;
using CondoAgents.Clients.Fakes;
using CondoAgents.Corpus;
using CondoAgents.Evals;
using CondoAgents.Ingest;

using Microsoft.Extensions.Logging.Abstractions;

var evalsRoot = EvalCases.FindEvalsRoot(AppContext.BaseDirectory) ?? throw new InvalidOperationException("evals/cases.json not found.");
var dataRoot = CorpusLoader.FindDataRoot(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/manifest.json not found.");

var index = new InMemoryIndex();
var pipeline = new IngestPipeline(new PlainTextExtractor(), new IdentityTranslator(), new RegexPiiDetector(), new KeywordRoleAdjudicator(), index, NullLogger<IngestPipeline>.Instance);
await CorpusIngestor.IngestAllAsync(dataRoot, pipeline, index);
var retrieval = new InMemoryRetrievalClient(index);

var runner = new EvalRunner(
    store => new DueDiligenceOrchestrator(new ScriptedChatClient(), retrieval, new RegexPiiDetector(), new PermissivePromptShield(), new RecordingMessenger(), store),
    mode: "fake",
    modelId: "scripted-fake");

var report = await runner.RunAsync(EvalCases.Load(Path.Combine(evalsRoot, "cases.json")));

var results = Path.Combine(evalsRoot, "results");
Directory.CreateDirectory(results);
await File.WriteAllTextAsync(Path.Combine(results, "latest.json"), Report.ToJson(report));
await File.WriteAllTextAsync(Path.Combine(results, "latest.md"), Report.ToMarkdown(report));

Console.WriteLine(Report.ToMarkdown(report));
return report.AllPassed ? 0 : 1;