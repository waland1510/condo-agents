using CondoAgents.Agents.Fakes;
using CondoAgents.Agents.Orchestration;
using CondoAgents.Agents.Tools;
using CondoAgents.Clients.Fakes;
using CondoAgents.Corpus;
using CondoAgents.Ingest;
using CondoAgents.Mcp;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

var scope = ScopedMcpTools.ScopeFromArgs(args);
var dataRoot = CorpusLoader.FindDataRoot(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/manifest.json not found.");

var index = new InMemoryIndex();
var pipeline = new IngestPipeline(new PlainTextExtractor(), new IdentityTranslator(), new RegexPiiDetector(), new KeywordRoleAdjudicator(), index, NullLogger<IngestPipeline>.Instance);
await CorpusIngestor.IngestAllAsync(dataRoot, pipeline, index);

var retrieval = new InMemoryRetrievalClient(index);
var shield = new PermissivePromptShield();
var toolbox = new ScopedToolbox(scope, retrieval, shield);
var orchestrator = new DueDiligenceOrchestrator(new ScriptedChatClient(), retrieval, new RegexPiiDetector(), shield, new RecordingMessenger(), new InMemoryRunStore());

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Services.AddMcpServer(o => o.ServerInfo = new() { Name = "condo-agents", Version = "0.1.0" })
    .WithStdioServerTransport()
    .WithTools(ScopedMcpTools.Build(toolbox, orchestrator));

await builder.Build().RunAsync();