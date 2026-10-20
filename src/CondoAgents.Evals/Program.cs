using CondoAgents.Agents.Fakes;
using CondoAgents.Agents.Orchestration;
using CondoAgents.Agents.Runtime;
using CondoAgents.Clients;
using CondoAgents.Clients.Azure;
using CondoAgents.Clients.Fakes;
using CondoAgents.Corpus;
using CondoAgents.Evals;
using CondoAgents.Ingest;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

var evalsRoot = EvalCases.FindEvalsRoot(AppContext.BaseDirectory) ?? throw new InvalidOperationException("evals/cases.json not found.");
var dataRoot = CorpusLoader.FindDataRoot(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/manifest.json not found.");
var mode = Environment.GetEnvironmentVariable("CONDO_EVAL_MODE") ?? "fake";

IChatClient model;
IRetrievalClient retrieval;
IIndexWriter writer;
IPiiDetector pii;
IPromptShield shield;
IRoleAdjudicator adjudicator;
string modelId;
RunBudget budget;

if (mode == "azure")
{
    static string Env(string name) => Environment.GetEnvironmentVariable(name) ?? throw new InvalidOperationException($"{name} is required in azure mode.");
    var options = new AzureOptions(
        Env("Ai__FoundryEndpoint"), Env("Ai__ChatDeployment"), Env("Ai__EmbeddingDeployment"), Env("Ai__SearchEndpoint"), Env("Ai__SearchIndexName"),
        Env("Ai__LanguageEndpoint"), Environment.GetEnvironmentVariable("Ai__ContentSafetyEndpoint") ?? "",
        Environment.GetEnvironmentVariable("Ai__TenantId"), Environment.GetEnvironmentVariable("Ai__AzureCliSubscription"));
    options.Validate();
    var credential = AzureCredentials.Create(options);
    var index = new AzureSearchIndex(options, credential, FoundryClients.Embeddings(options, credential));
    await index.EnsureIndexAsync();
    model = FoundryClients.Chat(options, credential);
    retrieval = index;
    writer = index;
    pii = new AzureLanguagePiiDetector(options, credential);
    shield = new AzureContentSafetyPromptShield(options, credential, new HttpClient());
    adjudicator = new ModelRoleAdjudicator(model);
    modelId = options.ChatDeployment;
    budget = RunBudget.Default with { MaxModelCalls = 30, MaxToolCalls = 40, MaxTotalTokens = 400_000, MaxDuration = TimeSpan.FromMinutes(10) };
}
else
{
    var index = new InMemoryIndex();
    model = new ScriptedChatClient();
    retrieval = new InMemoryRetrievalClient(index);
    writer = index;
    pii = new RegexPiiDetector();
    shield = new PermissivePromptShield();
    adjudicator = new KeywordRoleAdjudicator();
    modelId = "scripted-fake";
    budget = RunBudget.Default;
}

var pipeline = new IngestPipeline(new PlainTextExtractor(), new IdentityTranslator(), pii, adjudicator, writer, NullLogger<IngestPipeline>.Instance);
var ingested = await CorpusIngestor.IngestAllAsync(dataRoot, pipeline, writer);
Console.Error.WriteLine($"[{mode}] ingested {ingested.Count} documents, {ingested.Sum(r => r.ChunkCount)} chunks, {ingested.Sum(r => r.RedactedFindings)} redactions");

var runner = new EvalRunner(
    store => new DueDiligenceOrchestrator(model, retrieval, pii, shield, new RecordingMessenger(), store) { Observer = e => Console.Error.WriteLine($"  {e.Kind,-5} {e.Name,-24} {e.Detail}") },
    mode,
    modelId,
    budget);

var only = Environment.GetEnvironmentVariable("CONDO_EVAL_CASE");
var cases = EvalCases.Load(Path.Combine(evalsRoot, "cases.json")).Where(c => only is null || c.Id == only).ToList();
var report = await runner.RunAsync(cases);

var results = Path.Combine(evalsRoot, "results");
Directory.CreateDirectory(results);
var stem = (mode == "fake" ? "latest" : $"{mode}-{DateTime.UtcNow:yyyyMMdd}") + (only is null ? "" : $"-{only}");
await File.WriteAllTextAsync(Path.Combine(results, stem + ".json"), Report.ToJson(report));
await File.WriteAllTextAsync(Path.Combine(results, stem + ".md"), Report.ToMarkdown(report));

Console.WriteLine(Report.ToMarkdown(report));
return report.AllPassed ? 0 : 1;