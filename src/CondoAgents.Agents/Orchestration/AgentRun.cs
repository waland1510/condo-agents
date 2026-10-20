using System.Text.Json;
using System.Text.Json.Serialization;

using CondoAgents.Agents.Runtime;
using CondoAgents.Domain.Retrieval;
using CondoAgents.Domain.Scoping;

namespace CondoAgents.Agents.Orchestration;

[JsonConverter(typeof(JsonStringEnumConverter<RunStatus>))]
public enum RunStatus
{
    Planning,
    Running,
    AwaitingApproval,
    Completed,
    Rejected,
    Failed,
    Refused,
}

public sealed record Claim(string Text, IReadOnlyList<string> Labels);

public sealed record UnsupportedClaim(
    [property: JsonPropertyName("claim")] string Claim,
    [property: JsonPropertyName("reason")] string Reason);

/// <summary>A model may omit an empty array; Normalized never returns null lists, and a missing unsupported list with pass=false is treated as "nothing specific", which keeps the claims.</summary>
public sealed record CriticVerdict(
    [property: JsonPropertyName("pass")] bool Pass,
    [property: JsonPropertyName("supported")] IReadOnlyList<string>? Supported,
    [property: JsonPropertyName("unsupported")] IReadOnlyList<UnsupportedClaim>? Unsupported)
{
    public CriticVerdict Normalized() => this with
    {
        Supported = Supported ?? [],
        Unsupported = (Unsupported ?? []).Where(u => !string.IsNullOrWhiteSpace(u?.Claim)).Select(u => u with { Reason = u.Reason ?? "" }).ToList(),
    };
}

public sealed record PendingApproval(
    string Id,
    string StepId,
    string Tool,
    string ArgumentsJson,
    bool? Approved = null,
    string? DecidedBy = null,
    DateTimeOffset? DecidedAt = null);

public sealed class StepRecord
{
    public required string StepId { get; init; }

    public required string Specialist { get; init; }

    public required string Question { get; init; }

    public int Attempts { get; set; }

    public AgentAnswer? Answer { get; set; }

    public IReadOnlyList<Citation> Citations { get; set; } = [];

    public IReadOnlyList<string> RejectedLabels { get; set; } = [];

    public IReadOnlyList<RetrievedChunk> Evidence { get; set; } = [];

    public CriticVerdict? Critic { get; set; }

    public IReadOnlyList<Claim> KeptClaims { get; set; } = [];

    public bool Completed { get; set; }

    /// <summary>Serialized agent session while a step waits on a human, so a different process can resume it.</summary>
    public JsonElement? PausedSession { get; set; }
}

public sealed record FinalAnswer(string Text, IReadOnlyList<Citation> Citations, IReadOnlyList<UnsupportedClaim> Dropped);

/// <summary>Everything about a run lives here and is persisted after every transition; the orchestrator holds no state of its own.</summary>
public sealed class AgentRun
{
    public required string Id { get; init; }

    public required Scope Scope { get; init; }

    public required string Question { get; init; }

    public required RunBudget Budget { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public RunStatus Status { get; set; } = RunStatus.Planning;

    public Plan? Plan { get; set; }

    public List<StepRecord> Steps { get; } = [];

    public List<PendingApproval> Approvals { get; } = [];

    public List<RunEvent> Events { get; } = [];

    public FinalAnswer? Final { get; set; }

    public string? Error { get; set; }

    public static string NewId() => $"run-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6]}";
}