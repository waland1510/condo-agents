using System.Text.Json;
using System.Text.RegularExpressions;

namespace CondoAgents.Agents.Fakes;

/// <summary>Three mechanical checks standing in for the model critic: a label, every number present in the cited text, and no claim built on a rejected or pending item.</summary>
public static partial class FakeCritic
{
    private static readonly string[] NegationMarkers = ["rejetée", "rejected", "avant d'octroyer", "not yet awarded", "deferred", "reportée"];
    private static readonly string[] Acknowledgements = ["rejet", "rejected", "not awarded", "n'a pas", "was not", "pas encore", "pending", "defer", "en attente"];

    public static string VerdictJson(string payloadJson)
    {
        using var doc = JsonDocument.Parse(payloadJson);
        var evidence = doc.RootElement.GetProperty("evidence").EnumerateArray()
            .ToDictionary(e => e.GetProperty("label").GetString()!, e => e.GetProperty("text").GetString()!, StringComparer.Ordinal);

        var supported = new List<string>();
        var unsupported = new List<object>();
        foreach (var claim in doc.RootElement.GetProperty("claims").EnumerateArray())
        {
            var text = claim.GetProperty("text").GetString()!;
            var labels = claim.GetProperty("labels").EnumerateArray().Select(l => l.GetString()!).ToList();
            var cited = string.Join("\n", labels.Where(evidence.ContainsKey).Select(l => evidence[l]));

            if (labels.Count == 0 || cited.Length == 0)
            {
                unsupported.Add(new { claim = text, reason = "no evidence label" });
                continue;
            }

            var missing = Numbers().Matches(text).Select(m => m.Value).FirstOrDefault(n => !cited.Contains(n, StringComparison.Ordinal));
            if (missing is not null)
            {
                unsupported.Add(new { claim = text, reason = $"amount {missing} is not in the cited passage" });
                continue;
            }

            var lower = text.ToLowerInvariant();
            if (NegationMarkers.Any(m => cited.Contains(m, StringComparison.OrdinalIgnoreCase)) && !Acknowledgements.Any(lower.Contains))
            {
                unsupported.Add(new { claim = text, reason = "the cited passage describes a rejected or pending item" });
                continue;
            }

            supported.Add(text);
        }

        return JsonSerializer.Serialize(new { pass = unsupported.Count == 0, supported, unsupported });
    }

    [GeneratedRegex(@"\d[\d\s]{2,}\d")]
    private static partial Regex Numbers();
}