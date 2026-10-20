using System.Text.Json;

namespace CondoAgents.Agents.Fakes;

/// <summary>Keyword planner standing in for the model: broad questions fan out to every specialist, narrow ones to the one that fits.</summary>
public static class FakePlanner
{
    private static readonly string[] FinanceWords = ["fee", "frais", "cotisation", "assessment", "fund", "fonds", "budget", "cost", "coût", "$", "reserve", "prévoyance", "deductible", "franchise"];
    private static readonly string[] DocumentWords = ["pet", "animal", "chien", "dog", "rent", "location", "bylaw", "règlement", "minutes", "litige", "litigation", "lawsuit", "vote", "decision", "roof", "toiture", "charging", "borne", "syndicate", "contact"];
    private static readonly string[] LegalWords = ["code", "law", "legal", "article", "obligation", "right", "droit", "entitled"];

    public static string PlanJson(string question)
    {
        var q = question.ToLowerInvariant();
        var steps = new List<object>();
        var broad = q.Contains("safe") || q.Contains("should i buy") || q.Contains("risk") || q.Contains("overall");
        var contact = q.Contains("contact the syndicate");

        if (broad || FinanceWords.Any(q.Contains))
        {
            steps.Add(new { id = "finance", specialist = "finance", question = "What are the monthly fees, special assessments, contingency fund balance and any shortfall against the reserve-fund study?", dependsOn = Array.Empty<string>() });
        }

        if (broad || DocumentWords.Any(q.Contains))
        {
            steps.Add(new { id = "documents", specialist = "documents", question = broad ? "What decisions, disputes, litigation and bylaw changes affect a buyer?" : question, dependsOn = Array.Empty<string>(), contactSyndicate = contact });
        }

        if (broad || LegalWords.Any(q.Contains))
        {
            steps.Add(new { id = "legal", specialist = "legal", question = "What does the Civil Code require of the syndicate regarding the contingency fund and the documents owed to a buyer?", dependsOn = Array.Empty<string>() });
        }

        if (steps.Count == 0)
        {
            steps.Add(new { id = "documents", specialist = "documents", question, dependsOn = Array.Empty<string>(), contactSyndicate = contact });
        }

        return JsonSerializer.Serialize(new { goal = question, steps });
    }
}