using CondoAgents.Agents.Fakes;
using CondoAgents.Agents.Orchestration;
using CondoAgents.Agents.Tools;
using CondoAgents.Clients.Fakes;
using CondoAgents.Domain.Scoping;

namespace CondoAgents.Tests.Agents;

public sealed class HumanInTheLoopTests
{
    private static readonly Scope Scope = new("jardins-plateau");

    private const string Question = "Please contact the syndicate about the status of the roof contract.";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DueDiligenceOrchestrator Build(IRunStore store, RecordingMessenger messenger)
        => new(new ScriptedChatClient(), Fixture.Retrieval, new RegexPiiDetector(), new PermissivePromptShield(), messenger, store);

    [Fact]
    public async Task An_irreversible_tool_pauses_the_run_and_a_fresh_process_resumes_it_after_approval()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var messenger = new RecordingMessenger();
            var paused = await Build(new FileRunStore(dir.FullName), messenger).StartAsync(Scope, Question, ct: Ct);

            Assert.Equal(RunStatus.AwaitingApproval, paused.Status);
            var approval = Assert.Single(paused.Approvals);
            Assert.Equal(ScopedToolbox.SendQuestionToSyndic, approval.Tool);
            Assert.Null(approval.Approved);
            Assert.Empty(messenger.Sent);
            Assert.NotNull(paused.Steps[0].PausedSession);

            var resumed = await Build(new FileRunStore(dir.FullName), messenger).DecideAsync(paused.Id, approval.Id, approved: true, decidedBy: "buyer", Ct);

            Assert.Equal(RunStatus.Completed, resumed.Status);
            var sent = Assert.Single(messenger.Sent);
            Assert.Equal("syndicate:jardins-plateau", sent.Recipient);
            Assert.Contains("sent to the syndicate", resumed.Final!.Text, StringComparison.Ordinal);
            Assert.True(resumed.Approvals[0].Approved);
            Assert.Equal("buyer", resumed.Approvals[0].DecidedBy);
            Assert.Contains(resumed.Events, e => e.Kind == "gate" && e.Name == "approval");
            Assert.Contains(resumed.Events, e => e.Kind == "tool" && e.Name == ScopedToolbox.SendQuestionToSyndic);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task A_rejected_approval_sends_nothing_and_ends_the_run_as_rejected()
    {
        var store = new InMemoryRunStore();
        var messenger = new RecordingMessenger();
        var paused = await Build(store, messenger).StartAsync(Scope, Question, ct: Ct);

        var resumed = await Build(store, messenger).DecideAsync(paused.Id, paused.Approvals[0].Id, approved: false, decidedBy: "buyer", Ct);

        Assert.Equal(RunStatus.Rejected, resumed.Status);
        Assert.Empty(messenger.Sent);
        Assert.Contains("not approved", resumed.Final!.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(resumed.Events, e => e.Kind == "tool" && e.Name == ScopedToolbox.SendQuestionToSyndic);
    }

    [Fact]
    public async Task Deciding_twice_is_refused()
    {
        var store = new InMemoryRunStore();
        var messenger = new RecordingMessenger();
        var paused = await Build(store, messenger).StartAsync(Scope, Question, ct: Ct);
        await Build(store, messenger).DecideAsync(paused.Id, paused.Approvals[0].Id, true, "buyer", Ct);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Build(store, messenger).DecideAsync(paused.Id, paused.Approvals[0].Id, true, "buyer", Ct));
        Assert.Single(messenger.Sent);
    }
}