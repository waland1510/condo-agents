using CondoAgents.Clients;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CondoAgents.Agents.Runtime;

/// <summary>Builds every agent the same way: budgeted model client underneath, tool-call accounting and shielding on top.</summary>
public static class AgentFactory
{
    public static AIAgent Create(
        IChatClient model,
        string name,
        string instructions,
        IReadOnlyList<AITool> tools,
        RunBudget budget,
        RunTrace trace)
    {
        var budgeted = new BudgetedChatClient(model, budget, trace, name);
        var agent = new ChatClientAgent(budgeted, new ChatClientAgentOptions
        {
            Name = name,
            ChatOptions = new ChatOptions { Instructions = instructions, Tools = [.. tools] },
        });

        return agent.AsBuilder()
            .Use(async (_, context, next, ct) =>
            {
                if (trace.ToolCalls >= budget.MaxToolCalls)
                {
                    throw new BudgetExceededException("tool calls", budget.MaxToolCalls);
                }

                using var activity = Telemetry.Source.StartActivity("agent.tool");
                activity?.SetTag("tool.name", context.Function.Name);
                var started = DateTimeOffset.UtcNow;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var result = await next(context, ct);
                trace.Record(RunTrace.ToolCall, context.Function.Name, started, watch.Elapsed.TotalMilliseconds,
                    detail: string.Join(", ", context.Arguments.Select(a => $"{a.Key}={a.Value}")));
                return result;
            })
            .Build();
    }
}