using System.Collections.Concurrent;

using CondoAgents.Agents.Orchestration;

namespace CondoAgents.Api;

/// <summary>Runs continue on a background task so the browser can poll; a crash marks the run Failed instead of vanishing.</summary>
public sealed class RunLauncher(DueDiligenceOrchestrator orchestrator, IRunStore store, ILogger<RunLauncher> logger)
{
    private readonly ConcurrentDictionary<string, Task> _inFlight = new(StringComparer.Ordinal);

    public void Launch(AgentRun run) => Track(run.Id, () => orchestrator.ContinueAsync(run));

    public void LaunchDecision(string runId, string approvalId, bool approved, string decidedBy)
        => Track(runId, () => orchestrator.DecideAsync(runId, approvalId, approved, decidedBy));

    public bool IsInFlight(string runId) => _inFlight.TryGetValue(runId, out var task) && !task.IsCompleted;

    private void Track(string runId, Func<Task<AgentRun>> work)
    {
        var task = Task.Run(async () =>
        {
            try
            {
                await work();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Run {RunId} crashed", runId);
                if (await store.LoadAsync(runId) is { } run)
                {
                    run.Status = RunStatus.Failed;
                    run.Error = ex.Message;
                    await store.SaveAsync(run);
                }
            }
            finally
            {
                _inFlight.TryRemove(runId, out _);
            }
        });
        _inFlight[runId] = task;
    }
}