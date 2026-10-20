using CondoAgents.Agents.Fakes;
using CondoAgents.Agents.Orchestration;
using CondoAgents.Api;
using CondoAgents.Api.Configuration;
using CondoAgents.Clients;
using CondoAgents.Corpus;
using CondoAgents.Ingest;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<AiOptions>()
    .Bind(builder.Configuration.GetSection(AiOptions.Section))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<AiOptions>, AiOptionsValidator>();

var ai = builder.Configuration.GetSection(AiOptions.Section).Get<AiOptions>() ?? new AiOptions();
if (ai.UseAzure)
{
    throw new NotSupportedException("Azure clients are not wired yet; run with Ai:UseAzure=false in Development.");
}

builder.Services.AddFakeClients();
builder.Services.AddSingleton<IngestPipeline>();
builder.Services.AddSingleton<IChatClient, ScriptedChatClient>();
builder.Services.AddSingleton<IRunStore>(new FileRunStore(builder.Configuration["Runs:Path"] ?? Path.Combine(builder.Environment.ContentRootPath, "runs")));
builder.Services.AddSingleton<DueDiligenceOrchestrator>();

builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(Telemetry.ServiceName).AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddOtlpExporter())
    .WithMetrics(m => m.AddMeter(Telemetry.ServiceName).AddAspNetCoreInstrumentation().AddOtlpExporter());

var app = builder.Build();

var dataRoot = app.Configuration["Corpus:Path"] ?? CorpusLoader.FindDataRoot(app.Environment.ContentRootPath);
if (dataRoot is not null)
{
    var results = await CorpusIngestor.IngestAllAsync(
        dataRoot, app.Services.GetRequiredService<IngestPipeline>(), app.Services.GetRequiredService<IIndexWriter>());
    app.Logger.LogInformation("Seeded {Documents} documents, {Chunks} chunks from {DataRoot}", results.Count, results.Sum(r => r.ChunkCount), dataRoot);
}

app.MapRuns();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/health/ready", (IDependencyModeReporter mode, IHostEnvironment env) =>
{
    var payload = new { status = mode.Mode == DependencyMode.Azure || env.IsDevelopment() ? "ready" : "degraded", mode = mode.Mode.ToString().ToLowerInvariant() };
    return payload.status == "ready" ? Results.Ok(payload) : Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable);
});

await app.RunAsync();

public partial class Program;