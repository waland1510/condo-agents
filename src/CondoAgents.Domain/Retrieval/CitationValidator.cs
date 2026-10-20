namespace CondoAgents.Domain.Retrieval;

/// <summary>Evidence labels are resolved against what was retrieved, so a fabricated label never becomes a citation.</summary>
public static class CitationValidator
{
    public static CitationValidation Resolve(IReadOnlyList<string> claimedLabels, IReadOnlyList<RetrievedChunk> retrieved)
    {
        var byLabel = retrieved
            .Select((chunk, i) => (Label: RetrievedChunk.EvidenceLabel(i), Chunk: chunk))
            .ToDictionary(x => x.Label, x => x.Chunk, StringComparer.Ordinal);

        var citations = new List<Citation>();
        var rejected = new List<string>();
        foreach (var label in claimedLabels.Distinct(StringComparer.Ordinal))
        {
            if (byLabel.TryGetValue(label, out var chunk))
            {
                citations.Add(new Citation(label, chunk.DocumentId, chunk.Title, chunk.Page));
            }
            else
            {
                rejected.Add(label);
            }
        }

        return new CitationValidation(citations, rejected);
    }
}

public sealed record CitationValidation(IReadOnlyList<Citation> Citations, IReadOnlyList<string> RejectedLabels);