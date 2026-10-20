using CondoAgents.Clients;

namespace CondoAgents.Agents.Runtime;

public sealed record GateRefusal(string Gate, string Reason);

/// <summary>Deterministic checks that run before any model call; a refusal here costs zero tokens and leaves a trace event.</summary>
public sealed class Gates(IPiiDetector pii, IPromptShield shield, RunTrace trace)
{
    public async Task<GateRefusal?> CheckQuestionAsync(string question, CancellationToken ct = default)
    {
        var started = DateTimeOffset.UtcNow;
        var findings = await pii.DetectAsync(question, ct);
        if (findings.Count > 0)
        {
            trace.Record(RunTrace.Gate, "pii", started, 0, detail: $"refused: {findings.Count} finding(s)");
            return new GateRefusal("pii", "The question contains personal data; rephrase it without names or contact details.");
        }

        var verdict = await shield.AnalyzeAsync(question, [], ct);
        if (verdict.UserPromptAttack)
        {
            trace.Record(RunTrace.Gate, "prompt-shield", started, 0, detail: "refused");
            return new GateRefusal("prompt-shield", "The question looks like an attempt to change the assistant's instructions.");
        }

        trace.Record(RunTrace.Gate, "question", started, 0, detail: "passed");
        return null;
    }
}