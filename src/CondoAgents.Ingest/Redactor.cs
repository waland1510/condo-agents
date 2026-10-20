using System.Text;

namespace CondoAgents.Ingest;

public static class Redactor
{
    public static string Apply(string text, IEnumerable<RoleFinding> findings)
    {
        var builder = new StringBuilder(text.Length);
        var cursor = 0;
        foreach (var finding in findings.Where(f => f.Redact).OrderBy(f => f.Offset))
        {
            if (finding.Offset < cursor)
            {
                continue;
            }

            builder.Append(text, cursor, finding.Offset - cursor);
            builder.Append("[REDACTED:").Append(finding.Category).Append(']');
            cursor = finding.Offset + finding.Length;
        }

        builder.Append(text, cursor, text.Length - cursor);
        return builder.ToString();
    }
}