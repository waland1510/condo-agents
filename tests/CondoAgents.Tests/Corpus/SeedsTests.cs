using System.Text.Json;

using CondoAgents.Corpus;

namespace CondoAgents.Tests.Corpus;

/// <summary>Pins the seeds to the documents: a later edit of a document that silently removes a planted fact fails here, not in an eval.</summary>
public sealed class SeedsTests
{
    private static readonly JsonDocument Seeds = JsonDocument.Parse(File.ReadAllText(Path.Combine(TestData.Root, "seeds.json")));

    private static readonly Dictionary<string, string> Documents = CorpusManifest.Load(TestData.Root)
        .AllDocuments()
        .ToDictionary(d => d.Id, d => File.ReadAllText(Path.Combine(TestData.Root, d.File)), StringComparer.Ordinal);

    [Fact]
    public void Every_seeded_pii_value_is_present_in_its_document()
    {
        foreach (var seed in Seeds.RootElement.GetProperty("pii").EnumerateArray())
        {
            var document = seed.GetProperty("document").GetString()!;
            Assert.Contains(seed.GetProperty("text").GetString()!, Documents[document], StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Every_lure_document_carries_its_rejection_marker()
    {
        foreach (var lure in Seeds.RootElement.GetProperty("lures").EnumerateArray())
        {
            var document = lure.GetProperty("document").GetString()!;
            Assert.Contains(lure.GetProperty("marker").GetString()!, Documents[document], StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Facts_and_contradictions_name_documents_in_the_manifest()
    {
        var referenced = Seeds.RootElement.GetProperty("facts").EnumerateArray()
            .Concat(Seeds.RootElement.GetProperty("contradictions").EnumerateArray())
            .SelectMany(e => e.GetProperty("documents").EnumerateArray().Select(d => d.GetString()!));

        foreach (var id in referenced)
        {
            Assert.Contains(id, Documents.Keys);
        }
    }
}