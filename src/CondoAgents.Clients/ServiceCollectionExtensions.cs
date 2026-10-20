using CondoAgents.Clients.Fakes;
using CondoAgents.Domain.Retrieval;

using Microsoft.Extensions.DependencyInjection;

namespace CondoAgents.Clients;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFakeClients(this IServiceCollection services, IEnumerable<IndexedChunk>? seed = null)
    {
        var index = new InMemoryIndex(seed);
        services.AddSingleton(index);
        services.AddSingleton<IIndexWriter>(index);
        services.AddSingleton<IRetrievalClient>(new InMemoryRetrievalClient(index));
        services.AddSingleton<IRoleAdjudicator, KeywordRoleAdjudicator>();
        services.AddSingleton<IPiiDetector, RegexPiiDetector>();
        services.AddSingleton<IDocumentTextExtractor, PlainTextExtractor>();
        services.AddSingleton<IDocumentTranslator, IdentityTranslator>();
        services.AddSingleton<IPromptShield, PermissivePromptShield>();
        services.AddSingleton<IDependencyModeReporter, FakeDependencyModeReporter>();
        return services;
    }
}