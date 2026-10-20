namespace CondoAgents.Domain.Retrieval;

public sealed record RetrievedChunk(
    string ChunkId,
    string DocumentId,
    string Title,
    string Corpus,
    string Text,
    int? Page = null,
    double? Relevance = null)
{
    public static string EvidenceLabel(int position) => $"S{position + 1}";

    /// <summary>Labels are positional, so the order the model saw is the order the validator must use; no re-ranking after the fact.</summary>
    public static IReadOnlyList<RetrievedChunk> InEvidenceOrder(IReadOnlyList<RetrievedChunk> chunks) => chunks;
}