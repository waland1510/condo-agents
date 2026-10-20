using CondoAgents.Domain.Scoping;

namespace CondoAgents.Tests.Domain;

public sealed class ScopeFilterTests
{
    [Fact]
    public void Builds_building_and_reference_clauses()
    {
        var filter = ScopeFilter.Build(new Scope("maple-court"));

        Assert.Equal(
            "(building_id eq 'maple-court' and corpus eq 'building' and is_active eq true) or (corpus eq 'reference' and is_active eq true)",
            filter);
    }

    [Fact]
    public void Omits_reference_corpus_when_excluded()
    {
        var filter = ScopeFilter.Build(new Scope("maple-court", IncludeReferenceCorpus: false));

        Assert.DoesNotContain("reference", filter, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Maple Court")]
    [InlineData("x' or building_id ne 'y")]
    [InlineData("-leading")]
    public void Throws_instead_of_defaulting_open(string buildingId)
    {
        Assert.Throws<ScopeException>(() => ScopeFilter.Build(new Scope(buildingId)));
    }
}