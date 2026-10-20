using CondoAgents.Clients;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CondoAgents.Agents.Runtime;

/// <summary>Builds every agent the same way: budgeted model client underneath, tool-call accounting and shielding on top.</summary>
public static class AgentFactory
{
    /// <summary>The loop swallows tool exceptions as model-visible errors, so a budget violation is surfaced here, after the loop, as the exception it was.</summary>
    public static async Task<AgentResponse> RunCheckedAsync(this AIAgent agent, IEnumerable<ChatMessage> messages, AgentSession session, RunTrace trace, CancellationToken ct)
    {
        var response = await agent.RunAsync(messages, session, cancellationToken: ct);
        if (trace.Violation is { } violation)
        {
            throw violation;
        }

        return response;
    }

    public static async Task<AgentResponse<T>> RunCheckedAsync<T>(this AIAgent agent, string message, AgentSession session, RunTrace trace, CancellationToken ct)
    {
        var response = await agent.RunAsync<T>(message, session, cancellationToken: ct);
        if (trace.Violation is { } violation)
        {
            throw violation;
        }

        return response;
    }

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
                    var violation = new BudgetExceededException("tool calls", budget.MaxToolCalls);
                    trace.Violation ??= violation;
                    context.Terminate = true;
                    throw violation;
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