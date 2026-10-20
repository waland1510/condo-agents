using CondoAgents.Domain.Retrieval;

namespace CondoAgents.Clients;

/// <summary>The filter is applied by the search service before retrieval, so the model only ever sees entitled chunks.</summary>
public interface IRetrievalClient
{
    Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(string query, string filter, int top = 8, CancellationToken ct = default);
}

public sealed record PiiFinding(string Category, string Text, int Offset, int Length, double Confidence);

/// <summary>Deterministic detection that runs before any model call; the model never sees unredacted text.</summary>
public interface IPiiDetector
{
    Task<IReadOnlyList<PiiFinding>> DetectAsync(string text, CancellationToken ct = default);
}

public sealed record ExtractedPage(int Number, string Text);

public interface IDocumentTextExtractor
{
    Task<IReadOnlyList<ExtractedPage>> ExtractAsync(Stream content, string fileName, CancellationToken ct = default);
}

public sealed record Translation(string SourceLanguage, string Text);

public interface IDocumentTranslator
{
    Task<Translation> TranslateToEnglishAsync(string text, CancellationToken ct = default);
}

public sealed record PromptShieldResult(bool UserPromptAttack, IReadOnlyList<int> AttackedDocumentIndexes)
{
    public static readonly PromptShieldResult Clean = new(false, []);
}

/// <summary>Retrieved documents and tool outputs are untrusted model input.</summary>
public interface IPromptShield
{
    Task<PromptShieldResult> AnalyzeAsync(string userPrompt, IReadOnlyList<string> documents, CancellationToken ct = default);
}

public enum DependencyMode
{
    Fake,
    Azure,
}

public interface IDependencyModeReporter
{
    DependencyMode Mode { get; }
}