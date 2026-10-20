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
}