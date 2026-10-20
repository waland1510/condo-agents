using CondoAgents.Clients.Fakes;
using CondoAgents.Domain.Retrieval;
using CondoAgents.Domain.Scoping;

namespace CondoAgents.Tests.Clients;

public sealed class InMemoryRetrievalClientTests
{
    private static readonly InMemoryRetrievalClient Client = new(
    [
        new("a1", "maple-bylaws", "Maple bylaws", "building", "maple-court", "Pets allowed with a deposit."),
        new("b1", "oak-bylaws", "Oak bylaws", "building", "oak-towers", "Pets forbidden."),
        new("r1", "ccq-1063", "CCQ art. 1063", "reference", null, "Each owner may use pets as the bylaws allow."),
        new("a2", "maple-old", "Maple bylaws 2019", "building", "maple-court", "Pets forbidden entirely.", IsActive: false),
    ]);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Returns_only_the_scoped_building_and_the_reference_corpus()
    {
        var hits = await Client.RetrieveAsync("pets", ScopeFilter.Build(new Scope("maple-court")), ct: Ct);

        Assert.Equal(["a1", "r1"], hits.Select(h => h.ChunkId).Order());
    }

    [Fact]
    public async Task Excludes_reference_corpus_when_scope_says_so()
    {
        var hits = await Client.RetrieveAsync("pets", ScopeFilter.Build(new Scope("maple-court", IncludeReferenceCorpus: false)), ct: Ct);

        Assert.Equal(["a1"], hits.Select(h => h.ChunkId));
    }

    [Fact]
    public async Task Refuses_a_filter_without_a_building_clause()
    {
        await Assert.ThrowsAsync<ScopeException>(() => Client.RetrieveAsync("pets", "is_active eq true", ct: Ct));
    }
}