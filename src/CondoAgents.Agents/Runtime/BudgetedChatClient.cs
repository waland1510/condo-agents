using System.Diagnostics;
using System.Runtime.CompilerServices;

using CondoAgents.Clients;

using Microsoft.Extensions.AI;

namespace CondoAgents.Agents.Runtime;

/// <summary>Sits under the function-calling loop, so the ceiling holds however many turns the model asks for.</summary>
public sealed class BudgetedChatClient(IChatClient inner, RunBudget budget, RunTrace trace, string agentName) : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Enforce();
        using var activity = Telemetry.Source.StartActivity("agent.model");
        activity?.SetTag("agent.name", agentName);
        var started = DateTimeOffset.UtcNow;
        var watch = Stopwatch.StartNew();

        var response = await base.GetResponseAsync(messages, options, cancellationToken);

        var input = response.Usage?.InputTokenCount ?? 0;
        var output = response.Usage?.OutputTokenCount ?? 0;
        trace.Record(RunTrace.ModelCall, agentName, started, watch.Elapsed.TotalMilliseconds, input, output,
            response.FinishReason?.ToString());
        activity?.SetTag("gen_ai.usage.input_tokens", input);
        activity?.SetTag("gen_ai.usage.output_tokens", output);
        if (trace.TotalTokens > budget.MaxTotalTokens)
        {
            throw new BudgetExceededException("total tokens", budget.MaxTotalTokens);
        }

        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Enforce();
        var started = DateTimeOffset.UtcNow;
        var watch = Stopwatch.StartNew();
        long input = 0, output = 0;
        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            foreach (var usage in update.Contents.OfType<UsageContent>())
            {
                input += usage.Details.InputTokenCount ?? 0;
                output += usage.Details.OutputTokenCount ?? 0;
            }

            yield return update;
        }

        trace.Record(RunTrace.ModelCall, agentName, started, watch.Elapsed.TotalMilliseconds, input, output);
    }

    private void Enforce()
    {
        if (trace.ModelCalls >= budget.MaxModelCalls)
        {
            throw new BudgetExceededException("model calls", budget.MaxModelCalls);
        }
    }
}