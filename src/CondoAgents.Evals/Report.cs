using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CondoAgents.Evals;

public static class Report
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static string ToJson(EvalReport report) => JsonSerializer.Serialize(report, Json);

    public static string ToMarkdown(EvalReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# Eval results · {report.Mode} · {report.ModelId}");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Ran {report.RanAt:yyyy-MM-dd HH:mm} UTC · {report.Passed} passed · {report.Failed} failed");
        sb.AppendLine();
        sb.AppendLine("| Case | Result | Status | Model calls | Tool calls | Tokens | ms |");
        sb.AppendLine("|---|---|---|---:|---:|---:|---:|");
        foreach (var c in report.Cases)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"| {c.Id} | {(c.Passed ? "pass" : "FAIL")} | {c.Status} | {c.ModelCalls} | {c.ToolCalls} | {c.Tokens} | {c.DurationMs:F0} |");
        }

        foreach (var c in report.Cases)
        {
            sb.AppendLine();
            sb.AppendLine(CultureInfo.InvariantCulture, $"## {c.Id}");
            sb.AppendLine();
            foreach (var check in c.Checks)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"- {(check.Passed ? "✓" : "✗")} `{check.Check}` — {check.Detail}");
            }
        }

        return sb.ToString();
    }
}