using CondoAgents.Clients.Fakes;
using CondoAgents.Corpus;
using CondoAgents.Domain.Scoping;

namespace CondoAgents.Tests.Corpus;

public sealed class CorpusLoaderTests
{
    private static readonly InMemoryRetrievalClient Client = new(CorpusLoader.LoadChunks(TestData.Root));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Both_bylaw_versions_are_retrievable_so_the_contradiction_is_visible()
    {
        var hits = await Client.RetrieveAsync("animaux chien", ScopeFilter.Build(new Scope("jardins-plateau")), ct: Ct);

        var documents = hits.Select(h => h.DocumentId).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("bylaws-2019", documents);
        Assert.Contains("bylaws-2024", documents);
    }

    [Fact]
    public async Task Reference_corpus_is_reachable_through_the_scope()
    {
        var hits = await Client.RetrieveAsync("contingency fund study", ScopeFilter.Build(new Scope("jardins-plateau")), ct: Ct);

        Assert.Contains(hits, h => h.Corpus == ScopeFilter.ReferenceCorpus && h.Text.Contains("Art. 1071", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Another_building_sees_nothing_but_the_reference_corpus()
    {
        var hits = await Client.RetrieveAsync("toiture fonds", ScopeFilter.Build(new Scope("oak-towers")), ct: Ct);

        Assert.All(hits, h => Assert.Equal(ScopeFilter.ReferenceCorpus, h.Corpus));
    }

    [Fact]
    public void Chunk_ids_are_unique_and_carry_year_and_building()
    {
        var chunks = CorpusLoader.LoadChunks(TestData.Root);

        Assert.Equal(chunks.Count, chunks.Select(c => c.ChunkId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(chunks, c => Assert.NotNull(c.Year));
        Assert.All(chunks.Where(c => c.Corpus == ScopeFilter.BuildingCorpus), c => Assert.Equal("jardins-plateau", c.BuildingId));
    }
}