using CondoAgents.Agents.Fakes;
using CondoAgents.Agents.Orchestration;
using CondoAgents.Clients.Fakes;
using CondoAgents.Domain.Scoping;

using Microsoft.Extensions.AI;

namespace CondoAgents.Tests.Agents;

public sealed class OrchestratorTests
{
    private static readonly Scope Scope = new("jardins-plateau");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DueDiligenceOrchestrator Build(IRunStore store, ScriptedChatClient.Script? script = null, RecordingMessenger? messenger = null)
        => new(new ScriptedChatClient(script), Fixture.Retrieval, new RegexPiiDetector(), new PermissivePromptShield(), messenger ?? new RecordingMessenger(), store);

    [Fact]
    public async Task A_broad_question_fans_out_to_every_specialist_and_completes_with_citations()
    {
        var store = new InMemoryRunStore();

        var run = await Build(store).StartAsync(Scope, "Is this condo safe to buy overall?", ct: Ct);

        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.Equal(["finance", "documents", "legal"], run.Plan!.Steps.Select(s => s.Specialist));
        Assert.All(run.Steps, s => Assert.True(s.Completed));
        Assert.NotEmpty(run.Final!.Citations);
        Assert.Contains(run.Final.Citations, c => c.DocumentId == "ccq-divided-co-ownership");
        Assert.Contains(run.Events, e => e.Kind == "model" && e.Name == "planner");
        Assert.Contains(run.Events, e => e.Kind == "model" && e.Name == "critic");
        Assert.Contains(run.Events, e => e.Kind == "tool" && e.Name == "civil_code_lookup");
        Assert.NotNull(await store.LoadAsync(run.Id, Ct));
    }

    [Fact]
    public async Task The_critic_catches_the_rejected_proposal_lure_and_the_claim_is_dropped()
    {
        var store = new InMemoryRunStore();
        var script = new ScriptedChatClient.Script(
            FirstCall: (_, tools) => new FunctionCallContent("c1", "search_documents", new Dictionary<string, object?> { ["query"] = "bornes recharge véhicules électriques" }),
            FinalJson: (_, results) =>
            {
                var first = results.SelectMany(r => ScriptedChatClient.ParsePassages(r.Json)).First();
                return $$"""{"kind":"answer","text":"The syndicate installed 12 electric vehicle charging stations in the garage for 120 000 $. [{{first.Label}}]","evidence":["{{first.Label}}"]}""";
            });

        var run = await Build(store, script).StartAsync(Scope, "Did the building install EV charging stations?", ct: Ct);

        var step = Assert.Single(run.Steps);
        Assert.Equal(DueDiligenceOrchestrator.MaxAttemptsPerStep, step.Attempts);
        Assert.False(step.Critic!.Pass);
        Assert.Contains(step.Critic.Unsupported!, u => u.Reason.Contains("rejected", StringComparison.Ordinal));
        Assert.Empty(step.KeptClaims);
        Assert.DoesNotContain("installed", run.Final!.Text, StringComparison.Ordinal);
        Assert.NotEmpty(run.Final.Dropped);
        Assert.Equal(2, run.Events.Count(e => e.Kind == "model" && e.Name == "critic"));
    }

    [Fact]
    public async Task A_question_with_personal_data_is_refused_without_planning()
    {
        var store = new InMemoryRunStore();

        var run = await Build(store).StartAsync(Scope, "Is Mme Lucie Pham still suing?", ct: Ct);

        Assert.Equal(RunStatus.Refused, run.Status);
        Assert.Null(run.Plan);
        Assert.DoesNotContain(run.Events, e => e.Kind == "model");
    }

    [Fact]
    public async Task A_run_round_trips_through_the_file_store()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var store = new FileRunStore(dir.FullName);
            var run = await Build(store).StartAsync(Scope, "What are the monthly fees?", ct: Ct);

            var loaded = await store.LoadAsync(run.Id, Ct);

            Assert.NotNull(loaded);
            Assert.Equal(run.Status, loaded.Status);
            Assert.Equal(run.Plan!.Steps.Count, loaded.Plan!.Steps.Count);
            Assert.Equal(run.Final!.Text, loaded.Final!.Text);
            Assert.Equal(run.Events.Count, loaded.Events.Count);
            Assert.Equal(run.Steps[0].Citations.Count, loaded.Steps[0].Citations.Count);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("""{"goal":"g","steps":[{"id":"a","specialist":"wizard","question":"q","dependsOn":[]}]}""")]
    [InlineData("""{"goal":"g","steps":[{"id":"a","specialist":"finance","question":"q","dependsOn":["b"]},{"id":"b","specialist":"legal","question":"q","dependsOn":["a"]}]}""")]
    [InlineData("""{"goal":"g","steps":[]}""")]
    public void Invalid_plans_are_rejected_before_any_step_runs(string json)
    {
        var plan = System.Text.Json.JsonSerializer.Deserialize<Plan>(json, RunJson.Options)!;

        Assert.Throws<InvalidPlanException>(plan.Validate);
    }
}