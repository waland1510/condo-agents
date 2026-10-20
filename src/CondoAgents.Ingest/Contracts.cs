using CondoAgents.Domain.Scoping;

namespace CondoAgents.Ingest;

/// <summary>The scope comes from the caller's resolved authorization, never from the document; the pipeline can only narrow it.</summary>
public sealed record IngestRequest(
    Scope Scope,
    string DocumentId,
    string Title,
    string FileName,
    int Year,
    Func<Stream> OpenContent);

public sealed record RoleFinding(string Category, string Text, int Offset, int Length, string Role, bool Redact);

/// <summary>Hash covers the exact redacted text that would be indexed; the attestation binds to it, not to the document id.</summary>
public sealed record ScreeningReport(
    string DocumentId,
    string SourceLanguage,
    IReadOnlyList<RoleFinding> Findings,
    string RedactedText,
    string ScreeningHash)
{
    public bool RequiresAttestation => Findings.Count > 0;
}

public sealed record Attestation(string ScreeningHash, string AttestedBy, DateTimeOffset At);

public sealed record IngestResult(string DocumentId, int ChunkCount, int RedactedFindings);

public sealed class AttestationMismatchException(string message) : InvalidOperationException(message);