using System.Text.RegularExpressions;

namespace CondoAgents.Clients.Fakes;

/// <summary>Regex stand-in for Azure AI Language: enough to exercise the gate, not a detector.</summary>
public sealed partial class RegexPiiDetector : IPiiDetector
{
    public Task<IReadOnlyList<PiiFinding>> DetectAsync(string text, CancellationToken ct = default)
    {
        var findings = new List<PiiFinding>();
        foreach (Match m in Email().Matches(text))
        {
            findings.Add(new PiiFinding("Email", m.Value, m.Index, m.Length, 0.99));
        }

        foreach (Match m in Phone().Matches(text))
        {
            findings.Add(new PiiFinding("PhoneNumber", m.Value, m.Index, m.Length, 0.9));
        }

        foreach (Match m in HonorificName().Matches(text))
        {
            var name = m.Groups["name"];
            findings.Add(new PiiFinding("Person", name.Value, name.Index, name.Length, 0.85));
        }

        foreach (Match m in SignatureName().Matches(text))
        {
            var name = m.Groups["name"];
            findings.Add(new PiiFinding("Person", name.Value, name.Index, name.Length, 0.8));
        }

        return Task.FromResult<IReadOnlyList<PiiFinding>>(findings.OrderBy(f => f.Offset).ToList());
    }

    [GeneratedRegex(@"[\w.+-]+@[\w-]+\.[\w.]+")]
    private static partial Regex Email();

    [GeneratedRegex(@"\b\d{3}[-. ]\d{3}[-. ]\d{4}\b")]
    private static partial Regex Phone();

    [GeneratedRegex(@"\b(?:M\.|Mme|Me|Dr)\s(?<name>\p{Lu}[\p{L}'-]+(?:\s\p{Lu}[\p{L}'-]+)?)")]
    private static partial Regex HonorificName();

    [GeneratedRegex(@"^(?<name>\p{Lu}[\p{L}'-]+(?:\s\p{Lu}[\p{L}'-]+)+),\s(?:ing\.|CPA|secrétaire|trésorier|présidente?)", RegexOptions.Multiline)]
    private static partial Regex SignatureName();
}

/// <summary>Keyword stand-in for the model adjudicator: owner markers win, then professional or director markers, else unknown (redact).</summary>
public sealed class KeywordRoleAdjudicator : IRoleAdjudicator
{
    private static readonly string[] OwnerMarkers = ["unité", "retard", "succession", "réclamant", "en souffrance", "vendu sa fraction"];
    private static readonly string[] ProfessionalMarkers = ["gestionnaire", "gestion immobilière", "avocat", "ing.", "ingénieur", "cpa", "notaire", "auditor", "me "];
    private static readonly string[] DirectorMarkers = ["présidente", "président", "trésorier", "secrétaire", "proposé par", "appuyé par", "élu", "réélu", "administrateur"];

    public Task<IReadOnlyList<RoleVerdict>> AdjudicateAsync(IReadOnlyList<RoleCandidate> candidates, CancellationToken ct = default)
    {
        var verdicts = candidates.Select(c =>
        {
            var context = c.Context.ToLowerInvariant();
            var role = OwnerMarkers.Any(context.Contains) ? RoleVerdict.Owner
                : ProfessionalMarkers.Any(context.Contains) ? RoleVerdict.Professional
                : DirectorMarkers.Any(context.Contains) ? RoleVerdict.Director
                : RoleVerdict.Unknown;
            return new RoleVerdict(c.Id, role);
        }).ToList();

        return Task.FromResult<IReadOnlyList<RoleVerdict>>(verdicts);
    }
}

public sealed class PlainTextExtractor : IDocumentTextExtractor
{
    public async Task<IReadOnlyList<ExtractedPage>> ExtractAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        using var reader = new StreamReader(content);
        var text = await reader.ReadToEndAsync(ct);
        var pages = text.Split("\f", StringSplitOptions.None);
        return pages.Select((p, i) => new ExtractedPage(i + 1, p)).ToList();
    }
}

public sealed class IdentityTranslator : IDocumentTranslator
{
    public Task<Translation> TranslateToEnglishAsync(string text, CancellationToken ct = default)
        => Task.FromResult(new Translation("en", text));
}

public sealed class PermissivePromptShield : IPromptShield
{
    public Task<PromptShieldResult> AnalyzeAsync(string userPrompt, IReadOnlyList<string> documents, CancellationToken ct = default)
        => Task.FromResult(PromptShieldResult.Clean);
}

public sealed class FakeDependencyModeReporter : IDependencyModeReporter
{
    public DependencyMode Mode => DependencyMode.Fake;
}