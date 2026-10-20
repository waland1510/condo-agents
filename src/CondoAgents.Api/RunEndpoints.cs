using CondoAgents.Agents.Orchestration;
using CondoAgents.Agents.Runtime;
using CondoAgents.Domain.Scoping;

namespace CondoAgents.Api;

public sealed record StartRunRequest(string Question, int? MaxToolCalls = null);

public sealed record DecisionRequest(bool Approved, string DecidedBy);

/// <summary>The building id in the route is the resolved scope; in a real deployment it comes from the caller's authorization, never from the body.</summary>
public static class RunEndpoints
{
    public static IEndpointRouteBuilder MapRuns(this IEndpointRouteBuilder app)
    {
        app.MapPost("/buildings/{buildingId}/runs", async (string buildingId, StartRunRequest body, DueDiligenceOrchestrator orchestrator, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Question))
            {
                return Results.BadRequest(new { error = "question is required" });
            }

            var budget = body.MaxToolCalls is { } max ? RunBudget.Default with { MaxToolCalls = max } : RunBudget.Default;
            try
            {
                var run = await orchestrator.StartAsync(new Scope(buildingId), body.Question, budget, ct);
                return Results.Created($"/runs/{run.Id}", run);
            }
            catch (ScopeException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapGet("/runs", async (IRunStore store, CancellationToken ct) =>
            Results.Ok((await store.ListAsync(ct)).Select(r => new { r.Id, r.Status, r.Question, r.CreatedAt, r.UpdatedAt })));

        app.MapGet("/runs/{runId}", async (string runId, IRunStore store, CancellationToken ct) =>
            await store.LoadAsync(runId, ct) is { } run ? Results.Ok(run) : Results.NotFound());

        app.MapPost("/runs/{runId}/approvals/{approvalId}", async (string runId, string approvalId, DecisionRequest body, DueDiligenceOrchestrator orchestrator, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await orchestrator.DecideAsync(runId, approvalId, body.Approved, body.DecidedBy, ct));
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        });

        return app;
    }
}