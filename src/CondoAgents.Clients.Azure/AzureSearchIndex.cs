using System.Text.Json.Serialization;

using Azure.Core;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;

using CondoAgents.Domain.Retrieval;
using CondoAgents.Domain.Scoping;

using Microsoft.Extensions.AI;

namespace CondoAgents.Clients.Azure;

/// <summary>Field names are the ones ScopeFilter emits, so the filter string works unchanged against the fake and against Search.</summary>
public sealed class SearchChunk
{
    [JsonPropertyName("chunk_id")]
    public required string ChunkId { get; set; }

    [JsonPropertyName("document_id")]
    public required string DocumentId { get; set; }

    [JsonPropertyName("title")]
    public required string Title { get; set; }

    [JsonPropertyName("corpus")]
    public required string Corpus { get; set; }

    [JsonPropertyName("building_id")]
    public string? BuildingId { get; set; }

    [JsonPropertyName("text")]
    public required string Text { get; set; }

    [JsonPropertyName("page")]
    public int? Page { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("year")]
    public int? Year { get; set; }

    [JsonPropertyName("text_vector")]
    public float[]? TextVector { get; set; }
}

/// <summary>Push-mode index owned by this app: no indexer, skillset or data source, so nothing on the shared service is shared.</summary>
public sealed class AzureSearchIndex(AzureOptions options, TokenCredential credential, IEmbeddingGenerator<string, Embedding<float>> embeddings)
    : IRetrievalClient, IIndexWriter
{
    private const string VectorProfile = "condo-agents-hnsw";

    private readonly SearchClient _client = new(new Uri(options.SearchEndpoint), options.SearchIndexName, credential);

    /// <summary>Creates once; an existing index is left alone so two processes starting together do not race on the definition.</summary>
    public async Task EnsureIndexAsync(CancellationToken ct = default)
    {
        options.Validate();
        var indexClient = new SearchIndexClient(new Uri(options.SearchEndpoint), credential);
        try
        {
            await indexClient.GetIndexAsync(options.SearchIndexName, ct);
            return;
        }
        catch (global::Azure.RequestFailedException ex) when (ex.Status == 404)
        {
        }

        var index = new SearchIndex(options.SearchIndexName)
        {
            Fields =
            {
                new SimpleField("chunk_id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true },
                new SimpleField("document_id", SearchFieldDataType.String) { IsFilterable = true },
                new SearchableField("title"),
                new SimpleField("corpus", SearchFieldDataType.String) { IsFilterable = true },
                new SimpleField("building_id", SearchFieldDataType.String) { IsFilterable = true },
                new SearchableField("text") { AnalyzerName = LexicalAnalyzerName.FrLucene },
                new SimpleField("page", SearchFieldDataType.Int32),
                new SimpleField("is_active", SearchFieldDataType.Boolean) { IsFilterable = true },
                new SimpleField("year", SearchFieldDataType.Int32) { IsFilterable = true },
                new VectorSearchField("text_vector", AzureOptions.EmbeddingDimensions, VectorProfile),
            },
            VectorSearch = new VectorSearch
            {
                Algorithms = { new HnswAlgorithmConfiguration("hnsw") },
                Profiles = { new VectorSearchProfile(VectorProfile, "hnsw") },
            },
        };
        await indexClient.CreateIndexAsync(index, ct);
    }

    public async Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(string query, string filter, int top = 8, CancellationToken ct = default)
    {
        var search = new SearchOptions { Filter = filter, Size = top, QueryType = SearchQueryType.Simple };
        if (!string.IsNullOrWhiteSpace(query))
        {
            var vector = await embeddings.GenerateVectorAsync(query, cancellationToken: ct);
            search.VectorSearch = new VectorSearchOptions { Queries = { new VectorizedQuery(vector) { KNearestNeighborsCount = top, Fields = { "text_vector" } } } };
        }

        foreach (var field in new[] { "chunk_id", "document_id", "title", "corpus", "text", "page" })
        {
            search.Select.Add(field);
        }

        var response = await _client.SearchAsync<SearchChunk>(string.IsNullOrWhiteSpace(query) ? "*" : query, search, ct);
        var results = new List<RetrievedChunk>();
        await foreach (var result in response.Value.GetResultsAsync().WithCancellation(ct))
        {
            var d = result.Document;
            results.Add(new RetrievedChunk(d.ChunkId, d.DocumentId, d.Title, d.Corpus, d.Text, d.Page, result.Score));
        }

        return results;
    }

    public async Task UpsertAsync(IReadOnlyList<IndexedChunk> chunks, CancellationToken ct = default)
    {
        if (chunks.Count == 0)
        {
            return;
        }

        var vectors = await embeddings.GenerateAsync(chunks.Select(c => c.Text).ToList(), cancellationToken: ct);
        var documents = chunks.Select((c, i) => new SearchChunk
        {
            ChunkId = c.ChunkId,
            DocumentId = c.DocumentId,
            Title = c.Title,
            Corpus = c.Corpus,
            BuildingId = c.BuildingId,
            Text = c.Text,
            Page = c.Page,
            IsActive = c.IsActive,
            Year = c.Year,
            TextVector = vectors[i].Vector.ToArray(),
        });
        await _client.MergeOrUploadDocumentsAsync(documents, cancellationToken: ct);
    }

    public async Task DeactivateAsync(string documentId, CancellationToken ct = default)
    {
        var existing = await RetrieveAsync(string.Empty, $"{ScopeFilter.DocumentField} eq '{documentId}'", top: 1000, ct);
        var updates = existing.Select(c => new { chunk_id = c.ChunkId, is_active = false });
        await _client.MergeDocumentsAsync(updates, cancellationToken: ct);
    }
}