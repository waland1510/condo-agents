using System.Text.RegularExpressions;

using CondoAgents.Agents.Runtime;

namespace CondoAgents.Agents.Orchestration;

/// <summary>Sentences with their evidence labels: the unit the critic judges and the synthesizer keeps or drops.</summary>
public static partial class Claims
{
    public static IReadOnlyList<Claim> Split(AgentAnswer answer)
    {
        if (answer.Kind != AgentAnswer.KindAnswer)
        {
            return [];
        }

        return Sentence().Split(answer.Text)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Select(s => new Claim(s, Label().Matches(s).Select(m => m.Groups["label"].Value).Distinct(StringComparer.Ordinal).ToList()))
            .ToList();
    }

    [GeneratedRegex(@"(?<!\b(?:Art|M|Mme|Me|inc|no|art|p)\.)(?<!\d\.)(?<=[.!?]\s*(?:\[S\d+\]\s*)*)\s+(?=\p{Lu}|\d)")]
    private static partial Regex Sentence();

    [GeneratedRegex(@"\[(?<label>S\d+)\]")]
    private static partial Regex Label();
}