using System.Text.Json.Serialization;

using CondoAgents.Domain.Retrieval;

namespace CondoAgents.Agents.Runtime;

/// <summary>Strict output shape: evidence is a list of labels the model saw, resolved by CitationValidator before anything is shown.</summary>
public sealed record AgentAnswer(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("evidence")] IReadOnlyList<string> Evidence)
{
    public const string KindAnswer = "answer";
    public const string KindRefusal = "refusal";
    public const string KindClarification = "clarification";
}

public sealed record AnswerResult(
    AgentAnswer Answer,
    IReadOnlyList<Citation> Citations,
    IReadOnlyList<string> RejectedLabels,
    IReadOnlyList<RetrievedChunk> Evidence,
    RunTrace Trace);