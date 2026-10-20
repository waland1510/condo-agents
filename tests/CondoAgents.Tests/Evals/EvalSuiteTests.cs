using CondoAgents.Agents.Fakes;
using CondoAgents.Agents.Orchestration;
using CondoAgents.Clients.Fakes;
using CondoAgents.Evals;
using CondoAgents.Tests.Agents;

namespace CondoAgents.Tests.Evals;

/// <summary>The committed eval cases are also a CI gate: on the fakes they are deterministic, so a regression in a gate or the critic fails here.</summary>
public sealed class EvalSuiteTests
{
    private static readonly string EvalsRoot = EvalCases.FindEvalsRoot(AppContext.BaseDirectory)!;

    public static TheoryData<string> CaseIds => [.. EvalCases.Load(Path.Combine(EvalsRoot, "cases.json")).Select(c => c.Id)];

    [Theory]
    [MemberData(nameof(CaseIds))]
    public async Task Case_passes_every_check(string id)
    {
        var @case = EvalCases.Load(Path.Combine(EvalsRoot, "cases.json")).Single(c => c.Id == id);
        var runner = new EvalRunner(
            store => new DueDiligenceOrchestrator(new ScriptedChatClient(), Fixture.Retrieval, new RegexPiiDetector(), new PermissivePromptShield(), new RecordingMessenger(), store),
            "fake", "scripted-fake");

        var result = await runner.RunCaseAsync(@case, TestContext.Current.CancellationToken);

        Assert.True(result.Passed, string.Join("\n", result.Checks.Where(c => !c.Passed).Select(c => $"{c.Check}: {c.Detail}")));
    }
}