using System.Collections.Concurrent;

namespace CondoAgents.Agents.Runtime;

public sealed record RunEvent(
    int Sequence,
    string Kind,
    string Name,
    DateTimeOffset At,
    double DurationMs,
    long InputTokens,
    long OutputTokens,
    string? Detail);

/// <summary>Append-only record of every model and tool call in a run; the trajectory the evals assert on.</summary>
public sealed class RunTrace(Action<RunEvent>? observer = null)
{
    public const string ModelCall = "model";
    public const string ToolCall = "tool";
    public const string Gate = "gate";

    private readonly ConcurrentQueue<RunEvent> _events = new();
    private int _sequence;
    private long _modelCalls;
    private long _toolCalls;
    private long _tokens;

    public IReadOnlyList<RunEvent> Events => [.. _events.OrderBy(e => e.Sequence)];

    /// <summary>Set by the tool middleware, which cannot throw past the function-calling loop; the agent runner checks it after every run.</summary>
    public BudgetExceededException? Violation { get; set; }

    public long ModelCalls => Interlocked.Read(ref _modelCalls);

    public long ToolCalls => Interlocked.Read(ref _toolCalls);

    public long TotalTokens => Interlocked.Read(ref _tokens);

    public void Record(string kind, string name, DateTimeOffset at, double durationMs, long inputTokens = 0, long outputTokens = 0, string? detail = null)
    {
        var sequence = Interlocked.Increment(ref _sequence);
        var record = new RunEvent(sequence, kind, name, at, durationMs, inputTokens, outputTokens, detail);
        _events.Enqueue(record);
        observer?.Invoke(record);
        if (kind == ModelCall)
        {
            Interlocked.Increment(ref _modelCalls);
            Interlocked.Add(ref _tokens, inputTokens + outputTokens);
        }
        else if (kind == ToolCall)
        {
            Interlocked.Increment(ref _toolCalls);
        }
    }
}