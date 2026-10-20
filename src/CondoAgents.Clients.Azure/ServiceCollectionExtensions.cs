using CondoAgents.Clients.Fakes;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CondoAgents.Clients.Azure;

public sealed class AzureDependencyModeReporter : IDependencyModeReporter
{
    public DependencyMode Mode => DependencyMode.Azure;
}

public static class ServiceCollectionExtensions
{
    /// <summary>Translation and document cracking stay on fakes: both need blob storage, and this app deliberately owns none.</summary>
    public static IServiceCollection AddAzureClients(this IServiceCollection services, AzureOptions options)
    {
        options.Validate();
        var credential = AzureCredentials.Create(options);
        var embeddings = FoundryClients.Embeddings(options, credential);
        var index = new AzureSearchIndex(options, credential, embeddings);

        services.AddSingleton(options);
        services.AddSingleton(credential);
        services.AddSingleton(embeddings);
        services.AddSingleton<IChatClient>(FoundryClients.Chat(options, credential));
        services.AddSingleton(index);
        services.AddSingleton<IRetrievalClient>(index);
        services.AddSingleton<IIndexWriter>(index);
        services.AddSingleton<IPiiDetector>(new AzureLanguagePiiDetector(options, credential));
        services.AddHttpClient<IPromptShield, AzureContentSafetyPromptShield>();
        services.AddSingleton<IDocumentTextExtractor, PlainTextExtractor>();
        services.AddSingleton<IDocumentTranslator, IdentityTranslator>();
        services.AddSingleton<IDependencyModeReporter, AzureDependencyModeReporter>();
        services.AddSingleton<IOutboundMessenger, RecordingMessenger>();
        return services;
    }
}