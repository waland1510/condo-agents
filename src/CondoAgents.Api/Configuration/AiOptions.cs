using CondoAgents.Clients.Azure;

namespace CondoAgents.Api.Configuration;

public sealed class AiOptions
{
    public const string Section = "Ai";

    /// <summary>On everywhere but Development: a deployment missing its Azure settings fails to boot instead of serving canned answers behind a healthy probe.</summary>
    public bool RequireRealDependencies { get; set; } = true;

    public bool UseAzure { get; set; }

    public string FoundryEndpoint { get; set; } = "";

    public string ChatDeployment { get; set; } = "";

    public string EmbeddingDeployment { get; set; } = "";

    public string SearchEndpoint { get; set; } = "";

    public string SearchIndexName { get; set; } = "";

    public string LanguageEndpoint { get; set; } = "";

    public string ContentSafetyEndpoint { get; set; } = "";

    public string? TenantId { get; set; }

    /// <summary>Local development only: pins the Azure CLI credential to the subscription whose tenant owns the resources.</summary>
    public string? AzureCliSubscription { get; set; }

    public AzureOptions ToAzureOptions() => new(
        FoundryEndpoint, ChatDeployment, EmbeddingDeployment, SearchEndpoint, SearchIndexName, LanguageEndpoint, ContentSafetyEndpoint, TenantId, AzureCliSubscription);
}