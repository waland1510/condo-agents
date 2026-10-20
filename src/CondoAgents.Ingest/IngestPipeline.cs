using System.Security.Cryptography;
using System.Text;

using CondoAgents.Clients;
using CondoAgents.Corpus;
using CondoAgents.Domain.Retrieval;
using CondoAgents.Domain.Scoping;

using Microsoft.Extensions.Logging;

namespace CondoAgents.Ingest;

/// <summary>Fixed order: crack, translate, detect, adjudicate, redact, hash. Indexing takes the screened report and refuses when the attestation or the source bytes no longer match; it never re-screens, because adjudication is model-backed and not byte-stable.</summary>
public sealed partial class IngestPipeline(
    IDocumentTextExtractor extractor,
    IDocumentTranslator translator,
    IPiiDetector piiDetector,
    IRoleAdjudicator adjudicator,
    IIndexWriter indexWriter,
    ILogger<IngestPipeline> logger)
{
    private const int ContextRadius = 120;

    public async Task<ScreeningReport> ScreenAsync(IngestRequest request, CancellationToken ct = default)
    {
        using var activity = Telemetry.Source.StartActivity("ingest.screen");
        activity?.SetTag("document.id", request.DocumentId);
        ScopeFilter.Build(request.Scope);

        var sourceHash = await HashSourceAsync(request, ct);
        await using var content = request.OpenContent();
        var pages = await extractor.ExtractAsync(content, request.FileName, ct);
        var text = string.Join("\f", pages.Select(p => p.Text));

        var translation = await translator.TranslateToEnglishAsync(text, ct);
        var detections = await piiDetector.DetectAsync(translation.Text, ct);

        var candidates = detections
            .Select((d, i) => new RoleCandidate($"f{i}", d.Category, d.Text, ContextAround(translation.Text, d.Offset, d.Length)))
            .ToList();
        var verdicts = (await adjudicator.AdjudicateAsync(candidates, ct)).ToDictionary(v => v.Id, StringComparer.Ordinal);

        var findings = detections.Select((d, i) =>
        {
            var role = verdicts.TryGetValue($"f{i}", out var v) ? v.Role : RoleVerdict.Unknown;
            var keep = verdicts.TryGetValue($"f{i}", out var verdict) && verdict.Keep;
            return new RoleFinding(d.Category, d.Text, d.Offset, d.Length, role, Redact: !keep);
        }).ToList();

        var redacted = Redactor.Apply(translation.Text, findings);
        var report = new ScreeningReport(request.DocumentId, translation.SourceLanguage, findings, redacted, Hash(request.DocumentId, redacted), sourceHash);

        Log.Screened(logger, request.DocumentId, findings.Count, findings.Count(f => f.Redact), report.ScreeningHash);
        activity?.SetTag("pii.findings", findings.Count);
        activity?.SetTag("pii.redacted", findings.Count(f => f.Redact));
        return report;
    }

    public async Task<IngestResult> IndexAsync(IngestRequest request, ScreeningReport report, Attestation attestation, CancellationToken ct = default)
    {
        using var activity = Telemetry.Source.StartActivity("ingest.index");
        activity?.SetTag("document.id", request.DocumentId);

        if (report.DocumentId != request.DocumentId || !string.Equals(report.ScreeningHash, attestation.ScreeningHash, StringComparison.Ordinal))
        {
            throw new AttestationMismatchException(
                $"Attestation for '{request.DocumentId}' covers hash {attestation.ScreeningHash}, but the screening report is {report.ScreeningHash}.");
        }

        var sourceHash = await HashSourceAsync(request, ct);
        if (!string.Equals(report.SourceHash, sourceHash, StringComparison.Ordinal))
        {
            throw new AttestationMismatchException(
                $"Source for '{request.DocumentId}' changed since it was screened ({report.SourceHash} → {sourceHash}); screen it again.");
        }

        var chunks = ParagraphChunker.Split(report.RedactedText)
            .Select((chunk, i) => new IndexedChunk(
                ChunkId: $"{request.DocumentId}__{i + 1}",
                DocumentId: request.DocumentId,
                Title: request.Title,
                Corpus: ScopeFilter.BuildingCorpus,
                BuildingId: request.Scope.BuildingId,
                Text: chunk.Text,
                Page: chunk.Page,
                IsActive: true,
                Year: request.Year))
            .ToList();

        await indexWriter.UpsertAsync(chunks, ct);
        activity?.SetTag("index.chunks", chunks.Count);
        return new IngestResult(request.DocumentId, chunks.Count, report.Findings.Count(f => f.Redact));
    }

    public Task DeindexAsync(string documentId, CancellationToken ct = default)
        => indexWriter.DeactivateAsync(documentId, ct);

    private static string ContextAround(string text, int offset, int length)
    {
        var start = Math.Max(0, offset - ContextRadius);
        var end = Math.Min(text.Length, offset + length + ContextRadius);
        return text[start..end];
    }

    private static async Task<string> HashSourceAsync(IngestRequest request, CancellationToken ct)
    {
        await using var content = request.OpenContent();
        var bytes = await SHA256.HashDataAsync(content, ct);
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    private static string Hash(string documentId, string redactedText)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(documentId + "\0" + redactedText));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Screened {DocumentId}: {Findings} findings, {Redacted} redacted, hash {Hash}")]
        public static partial void Screened(ILogger logger, string documentId, int findings, int redacted, string hash);
    }
}