using CondoAgents.Clients.Fakes;
using CondoAgents.Corpus;
using CondoAgents.Ingest;
using CondoAgents.Tests.Corpus;

using Microsoft.Extensions.Logging.Abstractions;

namespace CondoAgents.Tests.Agents;

/// <summary>One redacted index for every agent test, built through the real pipeline once.</summary>
internal static class Fixture
{
    public static InMemoryIndex Index { get; } = Build();

    public static InMemoryRetrievalClient Retrieval { get; } = new(Index);

    private static InMemoryIndex Build()
    {
        var index = new InMemoryIndex();
        var pipeline = new IngestPipeline(
            new PlainTextExtractor(), new IdentityTranslator(), new RegexPiiDetector(), new KeywordRoleAdjudicator(), index,
            NullLogger<IngestPipeline>.Instance);
        CorpusIngestor.IngestAllAsync(TestData.Root, pipeline, index).GetAwaiter().GetResult();
        return index;
    }
}