using CondoAgents.Corpus;

namespace CondoAgents.Tests.Corpus;

internal static class TestData
{
    public static string Root { get; } = CorpusLoader.FindDataRoot(AppContext.BaseDirectory)
        ?? throw new InvalidOperationException("data/manifest.json not found above the test output directory.");
}