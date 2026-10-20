using System.Text.RegularExpressions;

using CondoAgents.Domain.Retrieval;
using CondoAgents.Domain.Scoping;

namespace CondoAgents.Clients.Fakes;

/// <summary>Honours the scope filter so a scope test passes or fails the same way offline as against Azure.</summary>
public sealed partial class InMemoryRetrievalClient(InMemoryIndex index) : IRetrievalClient
{
    public InMemoryRetrievalClient(IEnumerable<IndexedChunk> chunks)
        : this(new InMemoryIndex(chunks))
    {
    }

    public Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(string query, string filter, int top = 8, CancellationToken ct = default)
    {
        var allowed = ParseFilter(filter);
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .ToArray();

        var hits = index.Snapshot()
            .Where(allowed)
            .Select(c => (Chunk: c, Score: terms.Count(t => c.Text.Contains(t, StringComparison.OrdinalIgnoreCase))))
            .Where(x => x.Score > 0 || terms.Length == 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Chunk.ChunkId, StringComparer.Ordinal)
            .Take(top)
            .Select(x => new RetrievedChunk(x.Chunk.ChunkId, x.Chunk.DocumentId, x.Chunk.Title, x.Chunk.Corpus, x.Chunk.Text, x.Chunk.Page, x.Score))
            .ToList();

        return Task.FromResult<IReadOnlyList<RetrievedChunk>>(hits);
    }

    private static Func<IndexedChunk, bool> ParseFilter(string filter)
    {
        var building = BuildingClause().Match(filter);
        if (!building.Success)
        {
            throw new ScopeException("Fake retrieval refuses a filter without a building clause.");
        }

        var buildingId = building.Groups["id"].Value;
        var includeReference = ReferenceClause().IsMatch(filter);
        return c => c.IsActive && (
            (c.Corpus == ScopeFilter.BuildingCorpus && c.BuildingId == buildingId) ||
            (includeReference && c.Corpus == ScopeFilter.ReferenceCorpus));
    }

    [GeneratedRegex(@"building_id eq '(?<id>[a-z0-9-]+)' and corpus eq 'building' and is_active eq true")]
    private static partial Regex BuildingClause();

    [GeneratedRegex(@"corpus eq 'reference' and is_active eq true")]
    private static partial Regex ReferenceClause();
}