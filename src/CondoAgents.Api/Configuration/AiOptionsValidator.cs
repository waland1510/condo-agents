using Microsoft.Extensions.Options;

namespace CondoAgents.Api.Configuration;

public sealed class AiOptionsValidator : IValidateOptions<AiOptions>
{
    public ValidateOptionsResult Validate(string? name, AiOptions options)
    {
        var failures = new List<string>();

        if (options.RequireRealDependencies && !options.UseAzure)
        {
            failures.Add("Ai:RequireRealDependencies is true but Ai:UseAzure is false; this environment would serve fake answers.");
        }

        if (options.UseAzure)
        {
            Require(failures, nameof(options.FoundryEndpoint), options.FoundryEndpoint);
            Require(failures, nameof(options.ChatDeployment), options.ChatDeployment);
            Require(failures, nameof(options.EmbeddingDeployment), options.EmbeddingDeployment);
            Require(failures, nameof(options.SearchEndpoint), options.SearchEndpoint);
            Require(failures, nameof(options.SearchIndexName), options.SearchIndexName);
            Require(failures, nameof(options.LanguageEndpoint), options.LanguageEndpoint);
            Require(failures, nameof(options.BlobServiceUri), options.BlobServiceUri);
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void Require(List<string> failures, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add($"Ai:{key} is required when Ai:UseAzure is true.");
        }
    }
}