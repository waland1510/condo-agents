using CondoAgents.Api.Configuration;

namespace CondoAgents.Tests.Api;

public sealed class AiOptionsValidatorTests
{
    private readonly AiOptionsValidator _validator = new();

    [Fact]
    public void Fails_when_real_dependencies_are_required_but_fakes_configured()
    {
        var result = _validator.Validate(null, new AiOptions { RequireRealDependencies = true, UseAzure = false });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("fake answers", StringComparison.Ordinal));
    }

    [Fact]
    public void Fails_when_azure_is_on_but_endpoints_are_missing()
    {
        var result = _validator.Validate(null, new AiOptions { UseAzure = true });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("FoundryEndpoint", StringComparison.Ordinal));
        Assert.Contains(result.Failures, f => f.Contains("SearchEndpoint", StringComparison.Ordinal));
    }

    [Fact]
    public void Succeeds_for_development_fakes()
    {
        var result = _validator.Validate(null, new AiOptions { RequireRealDependencies = false, UseAzure = false });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Refuses_an_index_name_outside_this_apps_prefix()
    {
        var result = _validator.Validate(null, new AiOptions
        {
            UseAzure = true,
            FoundryEndpoint = "https://f",
            ChatDeployment = "c",
            EmbeddingDeployment = "e",
            SearchEndpoint = "https://s",
            SearchIndexName = "iq-benefits-index",
            LanguageEndpoint = "https://l",
        });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("condo-agents-", StringComparison.Ordinal));
    }
}