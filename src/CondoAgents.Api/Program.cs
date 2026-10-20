using CondoAgents.Api.Configuration;
using CondoAgents.Clients;

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

builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddOtlpExporter())
    .WithMetrics(m => m.AddAspNetCoreInstrumentation().AddOtlpExporter());

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/health/ready", (IDependencyModeReporter mode, IHostEnvironment env) =>
{
    var payload = new { status = mode.Mode == DependencyMode.Azure || env.IsDevelopment() ? "ready" : "degraded", mode = mode.Mode.ToString().ToLowerInvariant() };
    return payload.status == "ready" ? Results.Ok(payload) : Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable);
});

app.Run();

public partial class Program;