using CondoAgents.Domain.Retrieval;
using CondoAgents.Domain.Scoping;

namespace CondoAgents.Corpus;

/// <summary>Reads the checked-in documents straight into index chunks; the real ingest pipeline replaces this with translate, redact, then index.</summary>
public static class CorpusLoader
{
    public static IReadOnlyList<IndexedChunk> LoadChunks(string dataRoot)
    {
        var manifest = CorpusManifest.Load(dataRoot);
        var chunks = new List<IndexedChunk>();

        foreach (var building in manifest.Buildings)
        {
            foreach (var document in building.Documents)
            {
                chunks.AddRange(ChunksFor(dataRoot, document, ScopeFilter.BuildingCorpus, building.Id));
            }
        }

        foreach (var document in manifest.Reference)
        {
            chunks.AddRange(ChunksFor(dataRoot, document, ScopeFilter.ReferenceCorpus, buildingId: null));
        }

        return chunks;
    }

    public static string? FindDataRoot(string startDirectory)
    {
        for (var dir = new DirectoryInfo(startDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "data");
            if (File.Exists(Path.Combine(candidate, CorpusManifest.FileName)))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<IndexedChunk> ChunksFor(string dataRoot, DocumentEntry document, string corpus, string? buildingId)
    {
        var text = File.ReadAllText(Path.Combine(dataRoot, document.File));
        return ParagraphChunker.Split(text).Select((chunk, i) => new IndexedChunk(
            ChunkId: $"{document.Id}__{i + 1}",
            DocumentId: document.Id,
            Title: document.Title,
            Corpus: corpus,
            BuildingId: buildingId,
            Text: chunk.Text,
            Page: chunk.Page,
            IsActive: true,
            Year: document.Year));
    }
}