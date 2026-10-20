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

    public string BlobServiceUri { get; set; } = "";
}