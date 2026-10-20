using Azure.AI.OpenAI;
using Azure.Core;

using Microsoft.Extensions.AI;

namespace CondoAgents.Clients.Azure;

public static class FoundryClients
{
    public static IChatClient Chat(AzureOptions options, TokenCredential credential)
        => new AzureOpenAIClient(new Uri(options.FoundryEndpoint), credential).GetChatClient(options.ChatDeployment).AsIChatClient();

    public static IEmbeddingGenerator<string, Embedding<float>> Embeddings(AzureOptions options, TokenCredential credential)
        => new AzureOpenAIClient(new Uri(options.FoundryEndpoint), credential).GetEmbeddingClient(options.EmbeddingDeployment).AsIEmbeddingGenerator(AzureOptions.EmbeddingDimensions);
}