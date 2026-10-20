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

        return Task.FromResult<IReadOnlyList<PiiFinding>>(findings);
    }

    [GeneratedRegex(@"[\w.+-]+@[\w-]+\.[\w.]+")]
    private static partial Regex Email();

    [GeneratedRegex(@"\b\d{3}[-. ]\d{3}[-. ]\d{4}\b")]
    private static partial Regex Phone();
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