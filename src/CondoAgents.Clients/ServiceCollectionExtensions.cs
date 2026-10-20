using CondoAgents.Clients.Fakes;

using Microsoft.Extensions.DependencyInjection;

namespace CondoAgents.Clients;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFakeClients(this IServiceCollection services, IEnumerable<IndexedChunk>? seed = null)
    {
        services.AddSingleton<IRetrievalClient>(new InMemoryRetrievalClient(seed ?? []));
        services.AddSingleton<IPiiDetector, RegexPiiDetector>();
        services.AddSingleton<IDocumentTextExtractor, PlainTextExtractor>();
        services.AddSingleton<IDocumentTranslator, IdentityTranslator>();
        services.AddSingleton<IPromptShield, PermissivePromptShield>();
        services.AddSingleton<IDependencyModeReporter, FakeDependencyModeReporter>();
        return services;
    }
}