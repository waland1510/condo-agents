using System.Text.Json.Serialization;

namespace CondoAgents.Agents.Orchestration;

public static class Specialists
{
    public const string Documents = "documents";
    public const string Finance = "finance";
    public const string Legal = "legal";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { Documents, Finance, Legal };
}

public sealed record PlanStep(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("specialist")] string Specialist,
    [property: JsonPropertyName("question")] string Question,
    [property: JsonPropertyName("dependsOn")] IReadOnlyList<string> DependsOn);

/// <summary>The planner's only output. Validation rejects unknown specialists and cycles before anything runs, so a bad plan costs one model call.</summary>
public sealed record Plan(
    [property: JsonPropertyName("goal")] string Goal,
    [property: JsonPropertyName("steps")] IReadOnlyList<PlanStep> Steps)
{
    public const int MaxSteps = 6;

    public IReadOnlyList<PlanStep> InExecutionOrder()
    {
        Validate();
        var done = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<PlanStep>();
        while (ordered.Count < Steps.Count)
        {
            var next = Steps.First(s => !done.Contains(s.Id) && s.DependsOn.All(done.Contains));
            ordered.Add(next);
            done.Add(next.Id);
        }

        return ordered;
    }

    public void Validate()
    {
        if (Steps.Count is 0 or > MaxSteps)
        {
            throw new InvalidPlanException($"A plan needs between 1 and {MaxSteps} steps; got {Steps.Count}.");
        }

        var ids = Steps.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        if (ids.Count != Steps.Count)
        {
            throw new InvalidPlanException("Step ids must be unique.");
        }

        foreach (var step in Steps)
        {
            if (!Specialists.All.Contains(step.Specialist))
            {
                throw new InvalidPlanException($"Unknown specialist '{step.Specialist}' in step '{step.Id}'.");
            }

            if (step.DependsOn.Any(d => !ids.Contains(d)))
            {
                throw new InvalidPlanException($"Step '{step.Id}' depends on a step that does not exist.");
            }
        }

        var done = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < Steps.Count; i++)
        {
            var next = Steps.FirstOrDefault(s => !done.Contains(s.Id) && s.DependsOn.All(done.Contains))
                ?? throw new InvalidPlanException("Plan has a dependency cycle.");
            done.Add(next.Id);
        }
    }
}

public sealed class InvalidPlanException(string message) : InvalidOperationException(message);