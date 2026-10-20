using System.Reflection;
using System.Text.Json.Serialization;

using CondoAgents.Clients.Azure;
using CondoAgents.Domain.Scoping;

namespace CondoAgents.Tests.Azure;

/// <summary>What keeps this app off another workload's resources on a shared Search service: a required index prefix and a filter grammar the index actually has fields for.</summary>
public sealed class AzureIsolationTests
{
    private static AzureOptions Options(string index) => new("https://f", "chat", "embed", "https://s", index, "https://l", "");

    [Theory]
    [InlineData("iq-benefits-index")]
    [InlineData("chunks")]
    [InlineData("")]
    public void Options_refuse_an_index_outside_the_prefix(string index)
    {
        Assert.Throws<InvalidOperationException>(() => Options(index).Validate());
    }

    [Fact]
    public void Options_accept_a_prefixed_index()
    {
        Options("condo-agents-chunks").Validate();
    }

    [Fact]
    public void Every_field_the_scope_filter_names_exists_on_the_search_document()
    {
        var fields = typeof(SearchChunk).GetProperties()
            .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()!.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(ScopeFilter.BuildingField, fields);
        Assert.Contains(ScopeFilter.DocumentField, fields);
        Assert.Contains(ScopeFilter.CorpusField, fields);
        Assert.Contains(ScopeFilter.ActiveField, fields);
    }
}