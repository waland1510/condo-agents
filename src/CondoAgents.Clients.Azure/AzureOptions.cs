namespace CondoAgents.Clients.Azure;

/// <summary>Endpoints only, never keys. The index name must carry this app's prefix so it can never collide with another workload on a shared Search service.</summary>
public sealed record AzureOptions(
    string FoundryEndpoint,
    string ChatDeployment,
    string EmbeddingDeployment,
    string SearchEndpoint,
    string SearchIndexName,
    string LanguageEndpoint,
    string ContentSafetyEndpoint,
    string? TenantId = null,
    string? AzureCliSubscription = null)
{
    public const string RequiredIndexPrefix = "condo-agents-";

    public const int EmbeddingDimensions = 3072;

    public void Validate()
    {
        if (!SearchIndexName.StartsWith(RequiredIndexPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Search index name must start with '{RequiredIndexPrefix}'; refusing to touch '{SearchIndexName}'.");
        }

        foreach (var (name, value) in new[]
        {
            (nameof(FoundryEndpoint), FoundryEndpoint), (nameof(ChatDeployment), ChatDeployment), (nameof(EmbeddingDeployment), EmbeddingDeployment),
            (nameof(SearchEndpoint), SearchEndpoint), (nameof(LanguageEndpoint), LanguageEndpoint),
        })
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Azure option '{name}' is required.");
            }
        }
    }

    public string EffectiveContentSafetyEndpoint => string.IsNullOrWhiteSpace(ContentSafetyEndpoint) ? LanguageEndpoint : ContentSafetyEndpoint;
}