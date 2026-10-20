using CondoAgents.Corpus;

namespace CondoAgents.Tests.Corpus;

public sealed class CorpusManifestTests
{
    private static readonly CorpusManifest Manifest = CorpusManifest.Load(TestData.Root);

    [Fact]
    public void Every_document_file_exists()
    {
        foreach (var document in Manifest.AllDocuments())
        {
            Assert.True(File.Exists(Path.Combine(TestData.Root, document.File)), $"{document.Id}: {document.File}");
        }
    }

    [Fact]
    public void Superseded_documents_point_at_a_document_in_the_same_building()
    {
        foreach (var building in Manifest.Buildings)
        {
            var ids = building.Documents.Select(d => d.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var document in building.Documents.Where(d => d.SupersededBy is not null))
            {
                Assert.Contains(document.SupersededBy!, ids);
            }
        }
    }

    [Fact]
    public void Rejects_duplicate_document_ids()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            File.WriteAllText(Path.Combine(dir.FullName, CorpusManifest.FileName), """
                { "buildings": [ { "id": "b", "name": "B", "address": "", "units": 1, "documents": [
                    { "id": "x", "file": "x.txt", "title": "X", "kind": "bylaws", "language": "fr", "year": 2020 } ] } ],
                  "reference": [ { "id": "x", "file": "x.txt", "title": "X", "kind": "statute", "language": "en", "year": 2020 } ] }
                """);

            Assert.Throws<InvalidDataException>(() => CorpusManifest.Load(dir.FullName));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}