using Azure.Core;
using Azure.Identity;

namespace CondoAgents.Clients.Azure;

/// <summary>Entra only. Locally the Azure CLI identity is pinned to one subscription (which implies its tenant; the CLI refuses both) because the developer's default tenant is not the one that owns the resources.</summary>
public static class AzureCredentials
{
    public static TokenCredential Create(AzureOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.AzureCliSubscription))
        {
            return new AzureCliCredential(new AzureCliCredentialOptions { Subscription = options.AzureCliSubscription });
        }

        return new DefaultAzureCredential(new DefaultAzureCredentialOptions { TenantId = options.TenantId });
    }
}