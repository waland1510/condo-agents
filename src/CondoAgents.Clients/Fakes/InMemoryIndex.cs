using CondoAgents.Domain.Retrieval;

namespace CondoAgents.Clients.Fakes;

/// <summary>One shared store so the fake writer and the fake retrieval client see the same chunks, as the Azure index would.</summary>
public sealed class InMemoryIndex : IIndexWriter
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, IndexedChunk> _chunks = new(StringComparer.Ordinal);

    public InMemoryIndex(IEnumerable<IndexedChunk>? seed = null)
    {
        foreach (var chunk in seed ?? [])
        {
            _chunks[chunk.ChunkId] = chunk;
        }
    }

    public IReadOnlyList<IndexedChunk> Snapshot()
    {
        lock (_gate)
        {
            return [.. _chunks.Values];
        }
    }

    public Task UpsertAsync(IReadOnlyList<IndexedChunk> chunks, CancellationToken ct = default)
    {
        lock (_gate)
        {
            foreach (var stale in _chunks.Values.Where(c => chunks.Any(n => n.DocumentId == c.DocumentId)).ToList())
            {
                _chunks.Remove(stale.ChunkId);
            }

            foreach (var chunk in chunks)
            {
                _chunks[chunk.ChunkId] = chunk;
            }
        }

        return Task.CompletedTask;
    }

    public Task DeactivateAsync(string documentId, CancellationToken ct = default)
    {
        lock (_gate)
        {
            foreach (var chunk in _chunks.Values.Where(c => c.DocumentId == documentId).ToList())
            {
                _chunks[chunk.ChunkId] = chunk with { IsActive = false };
            }
        }

        return Task.CompletedTask;
    }
}