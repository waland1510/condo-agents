using System.Text.Json;

using CondoAgents.Clients.Fakes;
using CondoAgents.Domain.Scoping;
using CondoAgents.Ingest;
using CondoAgents.Tests.Corpus;

using Microsoft.Extensions.Logging.Abstractions;

namespace CondoAgents.Tests.Ingest;

public sealed class CorpusIngestorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Seeded_index_holds_no_owner_pii_and_answers_scoped_queries()
    {
        var index = new InMemoryIndex();
        var pipeline = new IngestPipeline(
            new PlainTextExtractor(), new IdentityTranslator(), new RegexPiiDetector(), new KeywordRoleAdjudicator(), index,
            NullLogger<IngestPipeline>.Instance);

        var results = await CorpusIngestor.IngestAllAsync(TestData.Root, pipeline, index, Ct);

        var seeds = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(TestData.Root, "seeds.json"), Ct));
        var everything = string.Join("\n", index.Snapshot().Select(c => c.Text));
        foreach (var seed in seeds.RootElement.GetProperty("pii").EnumerateArray().Where(s => s.GetProperty("expect").GetString() == "redact"))
        {
            Assert.DoesNotContain(seed.GetProperty("text").GetString()!, everything, StringComparison.Ordinal);
        }

        Assert.Equal(7, results.Count);
        var client = new InMemoryRetrievalClient(index);
        var hits = await client.RetrieveAsync("toiture cotisation", ScopeFilter.Build(new Scope("jardins-plateau")), ct: Ct);
        Assert.Contains(hits, h => h.DocumentId == "agm-minutes-2023");
    }
}