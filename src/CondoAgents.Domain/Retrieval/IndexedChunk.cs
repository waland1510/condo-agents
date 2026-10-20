namespace CondoAgents.Domain.Retrieval;

/// <summary>BuildingId is null only for the shared reference corpus; the scope filter is what keeps the two apart.</summary>
public sealed record IndexedChunk(
    string ChunkId,
    string DocumentId,
    string Title,
    string Corpus,
    string? BuildingId,
    string Text,
    int? Page = null,
    bool IsActive = true,
    int? Year = null);