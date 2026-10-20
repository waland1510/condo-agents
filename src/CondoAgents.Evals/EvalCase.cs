using System.Text.Json;
using System.Text.Json.Serialization;

namespace CondoAgents.Evals;

public sealed record EvalCase(
    string Id,
    string Building,
    string Question,
    EvalExpectation Expect,
    int? MaxToolCalls = null);

/// <summary>Every field is a check on the trajectory or the outcome, never on prose; an omitted field is not checked.</summary>
public sealed record EvalExpectation(
    string? Status = null,
    IReadOnlyList<string>? Specialists = null,
    IReadOnlyList<string>? ToolsCalled = null,
    IReadOnlyList<string>? ToolsNotCalled = null,
    IReadOnlyList<string>? CitedDocumentsInclude = null,
    IReadOnlyList<string>? ForbiddenDocuments = null,
    IReadOnlyList<string>? FinalTextExcludes = null,
    string? CriticRejectedReasonIncludes = null,
    string? ErrorIncludes = null,
    int? PendingApprovals = null,
    int? MaxModelCalls = null,
    int? MaxModelCallsAzure = null);

public static class EvalCases
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static IReadOnlyList<EvalCase> Load(string path)
        => JsonSerializer.Deserialize<IReadOnlyList<EvalCase>>(File.ReadAllText(path), Json)
            ?? throw new InvalidDataException($"No eval cases in '{path}'.");

    public static string? FindEvalsRoot(string start)
    {
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "evals", "cases.json");
            if (File.Exists(candidate))
            {
                return Path.GetDirectoryName(candidate);
            }
        }

        return null;
    }
}

public sealed record CheckResult(string Check, bool Passed, string Detail);

public sealed record CaseResult(
    string Id,
    bool Passed,
    IReadOnlyList<CheckResult> Checks,
    string Status,
    long ModelCalls,
    long ToolCalls,
    long Tokens,
    double DurationMs,
    string RunId);

public sealed record EvalReport(
    DateTimeOffset RanAt,
    string Mode,
    string ModelId,
    int Passed,
    int Failed,
    IReadOnlyList<CaseResult> Cases)
{
    [JsonIgnore]
    public bool AllPassed => Failed == 0;
}