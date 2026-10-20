using System.Text;
using System.Text.Json;

using CondoAgents.Clients;
using CondoAgents.Clients.Fakes;
using CondoAgents.Corpus;
using CondoAgents.Domain.Scoping;
using CondoAgents.Ingest;
using CondoAgents.Tests.Corpus;

using Microsoft.Extensions.Logging.Abstractions;

namespace CondoAgents.Tests.Ingest;

public sealed class IngestPipelineTests
{
    private static readonly Scope Scope = new("jardins-plateau");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (IngestPipeline Pipeline, InMemoryIndex Index) Build()
    {
        var index = new InMemoryIndex();
        var pipeline = new IngestPipeline(
            new PlainTextExtractor(), new IdentityTranslator(), new RegexPiiDetector(), new KeywordRoleAdjudicator(), index,
            NullLogger<IngestPipeline>.Instance);
        return (pipeline, index);
    }

    private static IngestRequest RequestFor(string documentId)
    {
        var document = CorpusManifest.Load(TestData.Root).AllDocuments().Single(d => d.Id == documentId);
        return new IngestRequest(Scope, document.Id, document.Title, Path.GetFileName(document.File), document.Year,
            () => File.OpenRead(Path.Combine(TestData.Root, document.File)));
    }

    [Theory]
    [InlineData("agm-minutes-2023")]
    [InlineData("agm-minutes-2025")]
    [InlineData("reserve-fund-study-2022")]
    public async Task Screening_redacts_owners_and_keeps_professionals_per_the_seeds(string documentId)
    {
        var (pipeline, _) = Build();
        var seeds = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(TestData.Root, "seeds.json"), Ct));

        var report = await pipeline.ScreenAsync(RequestFor(documentId), Ct);

        foreach (var seed in seeds.RootElement.GetProperty("pii").EnumerateArray().Where(s => s.GetProperty("document").GetString() == documentId))
        {
            var text = seed.GetProperty("text").GetString()!;
            if (seed.GetProperty("expect").GetString() == "redact")
            {
                Assert.DoesNotContain(text, report.RedactedText, StringComparison.Ordinal);
            }
            else
            {
                Assert.Contains(text, report.RedactedText, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public async Task Indexing_requires_an_attestation_over_the_same_screening()
    {
        var (pipeline, index) = Build();
        var request = RequestFor("agm-minutes-2023");
        var report = await pipeline.ScreenAsync(request, Ct);

        await Assert.ThrowsAsync<AttestationMismatchException>(
            () => pipeline.IndexAsync(request, report, new Attestation("0000000000000000", "buyer", DateTimeOffset.UtcNow), Ct));
        Assert.Empty(index.Snapshot());

        var result = await pipeline.IndexAsync(request, report, new Attestation(report.ScreeningHash, "buyer", DateTimeOffset.UtcNow), Ct);

        Assert.True(result.ChunkCount > 0);
        Assert.All(index.Snapshot(), c => Assert.Equal("jardins-plateau", c.BuildingId));
        Assert.DoesNotContain(index.Snapshot(), c => c.Text.Contains("Tremblay-Rousseau", StringComparison.Ordinal));
        Assert.Contains(index.Snapshot(), c => c.Text.Contains("[REDACTED:Person]", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_edited_document_invalidates_the_earlier_attestation()
    {
        var (pipeline, _) = Build();
        var original = RequestFor("bylaws-2024");
        var report = await pipeline.ScreenAsync(original, Ct);
        var edited = original with { OpenContent = () => new MemoryStream(Encoding.UTF8.GetBytes("Contact Mme Lucie Pham, unité 408.")) };

        var ex = await Assert.ThrowsAsync<AttestationMismatchException>(
            () => pipeline.IndexAsync(edited, report, new Attestation(report.ScreeningHash, "buyer", DateTimeOffset.UtcNow), Ct));

        Assert.Contains("changed since it was screened", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_malformed_scope_is_refused_before_any_text_is_read()
    {
        var (pipeline, _) = Build();
        var request = RequestFor("bylaws-2024") with { Scope = new Scope("x' or 1 eq 1"), OpenContent = () => throw new InvalidOperationException("must not open") };

        await Assert.ThrowsAsync<ScopeException>(() => pipeline.ScreenAsync(request, Ct));
    }

    [Fact]
    public async Task Reindexing_replaces_and_deindexing_deactivates()
    {
        var (pipeline, index) = Build();
        var request = RequestFor("bylaws-2019");
        var report = await pipeline.ScreenAsync(request, Ct);
        var attestation = new Attestation(report.ScreeningHash, "buyer", DateTimeOffset.UtcNow);

        await pipeline.IndexAsync(request, report, attestation, Ct);
        await pipeline.IndexAsync(request, report, attestation, Ct);
        var afterTwice = index.Snapshot().Count;
        await pipeline.DeindexAsync("bylaws-2019", Ct);

        Assert.Equal(afterTwice, index.Snapshot().Count);
        Assert.All(index.Snapshot(), c => Assert.False(c.IsActive));
    }
}