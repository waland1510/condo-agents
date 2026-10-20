namespace CondoAgents.Agents.Runtime;

/// <summary>Hard ceilings per run; the model cannot raise them because they are enforced outside the model's loop.</summary>
public sealed record RunBudget(int MaxModelCalls = 12, int MaxToolCalls = 16, long MaxTotalTokens = 60_000, TimeSpan? MaxDuration = null)
{
    public static readonly RunBudget Default = new();

    public TimeSpan Duration => MaxDuration ?? TimeSpan.FromMinutes(2);
}

public sealed class BudgetExceededException(string what, long limit) : InvalidOperationException($"Run budget exceeded: {what} > {limit}.")
{
    public string What { get; } = what;

    public long Limit { get; } = limit;
}