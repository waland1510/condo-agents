using CondoAgents.Clients;
using CondoAgents.Corpus;
using CondoAgents.Domain.Retrieval;
using CondoAgents.Domain.Scoping;

namespace CondoAgents.Ingest;

/// <summary>Seeds the index through the real pipeline, self-attested because the corpus is synthetic; the reference corpus has no PII gate by definition.</summary>
public static class CorpusIngestor
{
    public const string SeedAttester = "seed:synthetic-corpus";

    public static async Task<IReadOnlyList<IngestResult>> IngestAllAsync(
        string dataRoot, IngestPipeline pipeline, IIndexWriter indexWriter, CancellationToken ct = default)
    {
        var manifest = CorpusManifest.Load(dataRoot);
        var results = new List<IngestResult>();

        foreach (var building in manifest.Buildings)
        {
            var scope = new Scope(building.Id);
            foreach (var document in building.Documents)
            {
                var request = new IngestRequest(
                    scope, document.Id, document.Title, Path.GetFileName(document.File), document.Year,
                    () => File.OpenRead(Path.Combine(dataRoot, document.File)));
                var report = await pipeline.ScreenAsync(request, ct);
                results.Add(await pipeline.IndexAsync(request, new Attestation(report.ScreeningHash, SeedAttester, DateTimeOffset.UtcNow), ct));
            }
        }

        foreach (var document in manifest.Reference)
        {
            var text = await File.ReadAllTextAsync(Path.Combine(dataRoot, document.File), ct);
            var chunks = ParagraphChunker.Split(text)
                .Select((chunk, i) => new IndexedChunk(
                    $"{document.Id}#{i + 1}", document.Id, document.Title, ScopeFilter.ReferenceCorpus, null, chunk.Text, chunk.Page, true, document.Year))
                .ToList();
            await indexWriter.UpsertAsync(chunks, ct);
            results.Add(new IngestResult(document.Id, chunks.Count, 0));
        }

        return results;
    }
}