using System.Text.Json;
using System.Text.Json.Serialization;

namespace CondoAgents.Corpus;

public sealed record CorpusManifest(IReadOnlyList<BuildingEntry> Buildings, IReadOnlyList<DocumentEntry> Reference)
{
    public const string FileName = "manifest.json";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static CorpusManifest Load(string dataRoot)
    {
        var path = Path.Combine(dataRoot, FileName);
        using var stream = File.OpenRead(path);
        var manifest = JsonSerializer.Deserialize<CorpusManifest>(stream, Json)
            ?? throw new InvalidDataException($"Empty corpus manifest at '{path}'.");

        var ids = manifest.AllDocuments().Select(d => d.Id).ToList();
        var duplicate = ids.GroupBy(id => id, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidDataException($"Document id '{duplicate.Key}' appears more than once in the manifest.");
        }

        return manifest;
    }

    public IEnumerable<DocumentEntry> AllDocuments()
        => Buildings.SelectMany(b => b.Documents).Concat(Reference);
}

public sealed record BuildingEntry(string Id, string Name, string Address, int Units, IReadOnlyList<DocumentEntry> Documents);

public sealed record DocumentEntry(
    string Id,
    string File,
    string Title,
    string Kind,
    string Language,
    int Year,
    [property: JsonPropertyName("supersededBy")] string? SupersededBy = null);