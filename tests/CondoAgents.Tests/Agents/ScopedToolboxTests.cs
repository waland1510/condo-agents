using System.Text.Json;

using CondoAgents.Agents.Tools;
using CondoAgents.Clients.Fakes;
using CondoAgents.Domain.Scoping;

using Microsoft.Extensions.AI;

namespace CondoAgents.Tests.Agents;

public sealed class ScopedToolboxTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void No_tool_exposes_the_boundary_as_an_argument()
    {
        var toolbox = new ScopedToolbox(new Scope("jardins-plateau"), Fixture.Retrieval, new PermissivePromptShield());
        string[] forbidden = ["building", "scope", "filter", "corpus", "building_id", "buildingId"];

        foreach (var function in toolbox.All().OfType<AIFunction>())
        {
            var properties = function.JsonSchema.TryGetProperty("properties", out var p) ? p.EnumerateObject().Select(o => o.Name).ToList() : [];
            Assert.DoesNotContain(properties, name => forbidden.Any(f => name.Contains(f, StringComparison.OrdinalIgnoreCase)));
        }
    }

    [Fact]
    public async Task Search_never_returns_another_building_even_when_asked_by_name()
    {
        var other = new InMemoryIndex(Fixture.Index.Snapshot().Concat(
        [
            new("oak#1", "oak-bylaws", "Oak Towers bylaws", "building", "oak-towers", "Oak Towers: pets are forbidden, chien interdit."),
        ]));
        var toolbox = new ScopedToolbox(new Scope("jardins-plateau"), new InMemoryRetrievalClient(other), new PermissivePromptShield());
        var search = toolbox.All().OfType<AIFunction>().Single(f => f.Name == ScopedToolbox.SearchDocuments);

        var result = await search.InvokeAsync(new AIFunctionArguments { ["query"] = "Oak Towers pets chien" }, Ct);

        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("oak-towers", json, StringComparison.Ordinal);
        Assert.DoesNotContain("oak-bylaws", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Open_document_matches_nothing_outside_the_scope()
    {
        var other = new InMemoryIndex(Fixture.Index.Snapshot().Concat(
        [
            new("oak#1", "oak-bylaws", "Oak Towers bylaws", "building", "oak-towers", "Oak Towers: pets are forbidden."),
        ]));
        var toolbox = new ScopedToolbox(new Scope("jardins-plateau"), new InMemoryRetrievalClient(other), new PermissivePromptShield());
        var open = toolbox.All().OfType<AIFunction>().Single(f => f.Name == ScopedToolbox.OpenDocument);

        var inside = JsonSerializer.Serialize(await open.InvokeAsync(new AIFunctionArguments { ["documentId"] = "bylaws-2024" }, Ct));
        var outside = JsonSerializer.Serialize(await open.InvokeAsync(new AIFunctionArguments { ["documentId"] = "oak-bylaws" }, Ct));

        Assert.Contains("bylaws-2024", inside, StringComparison.Ordinal);
        Assert.Equal("[]", outside);
    }

    [Fact]
    public async Task Open_document_rejects_a_malformed_id_instead_of_widening()
    {
        var toolbox = new ScopedToolbox(new Scope("jardins-plateau"), Fixture.Retrieval, new PermissivePromptShield());
        var open = toolbox.All().OfType<AIFunction>().Single(f => f.Name == ScopedToolbox.OpenDocument);

        await Assert.ThrowsAsync<ScopeException>(() => open.InvokeAsync(new AIFunctionArguments { ["documentId"] = "x' or building_id ne 'y" }, Ct).AsTask());
    }

    [Fact]
    public async Task Labels_stay_stable_across_calls()
    {
        var toolbox = new ScopedToolbox(new Scope("jardins-plateau"), Fixture.Retrieval, new PermissivePromptShield());
        var search = toolbox.All().OfType<AIFunction>().Single(f => f.Name == ScopedToolbox.SearchDocuments);

        var first = JsonSerializer.Serialize(await search.InvokeAsync(new AIFunctionArguments { ["query"] = "toiture" }, Ct));
        var second = JsonSerializer.Serialize(await search.InvokeAsync(new AIFunctionArguments { ["query"] = "toiture membrane" }, Ct));

        var firstLabels = JsonDocument.Parse(first).RootElement.EnumerateArray().ToDictionary(e => e.GetProperty("text").GetString()!, e => e.GetProperty("label").GetString());
        foreach (var e in JsonDocument.Parse(second).RootElement.EnumerateArray())
        {
            var key = e.GetProperty("text").GetString()!;
            if (firstLabels.TryGetValue(key, out var label))
            {
                Assert.Equal(label, e.GetProperty("label").GetString());
            }
        }

        Assert.Equal(toolbox.Evidence.Count, toolbox.Evidence.Select(c => c.ChunkId).Distinct().Count());
    }
}