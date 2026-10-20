using System.ComponentModel;

using CondoAgents.Agents.Orchestration;
using CondoAgents.Agents.Tools;
using CondoAgents.Domain.Scoping;

using Microsoft.Extensions.AI;

using ModelContextProtocol.Server;

namespace CondoAgents.Mcp;

/// <summary>The MCP surface is the same scoped toolbox plus one "ask" that runs the full orchestration; the client never names a building.</summary>
public static class ScopedMcpTools
{
    public static IReadOnlyList<McpServerTool> Build(ScopedToolbox toolbox, DueDiligenceOrchestrator orchestrator)
    {
        var tools = toolbox.All()
            .OfType<AIFunction>()
            .Select(f => McpServerTool.Create(f))
            .ToList();

        tools.Add(McpServerTool.Create(
            AIFunctionFactory.Create(
                async ([Description("The buyer's question about the building.")] string question, CancellationToken ct) =>
                {
                    var run = await orchestrator.StartAsync(toolbox.Scope, question, ct: ct);
                    return new
                    {
                        run.Id,
                        run.Status,
                        plan = run.Plan?.Steps.Select(s => new { s.Id, s.Specialist, s.Question }),
                        answer = run.Final?.Text,
                        citations = run.Final?.Citations.Select(c => new { c.Label, c.DocumentId, c.Title, c.Page }),
                        dropped = run.Final?.Dropped,
                        approvals = run.Approvals.Where(a => a.Approved is null).Select(a => new { a.Id, a.Tool, a.ArgumentsJson }),
                        modelCalls = run.Events.Count(e => e.Kind == "model"),
                        toolCalls = run.Events.Count(e => e.Kind == "tool"),
                    };
                },
                "ask_due_diligence",
                "Run the planner, specialists and critic over this building's documents and return the verified answer with citations, dropped claims and any pending approval.")));

        return tools;
    }

    public static Scope ScopeFromArgs(string[] args)
    {
        var index = Array.IndexOf(args, "--building");
        var buildingId = index >= 0 && index + 1 < args.Length ? args[index + 1] : Environment.GetEnvironmentVariable("CONDO_BUILDING") ?? "jardins-plateau";
        return new Scope(buildingId);
    }
}