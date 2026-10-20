using CondoAgents.Domain.Retrieval;

namespace CondoAgents.Tests.Domain;

public sealed class CitationValidatorTests
{
    private static readonly RetrievedChunk[] Retrieved =
    [
        new("c1", "bylaws-2024", "Bylaws 2024", "building", "Pets are allowed.", Page: 3),
        new("c2", "agm-2025", "AGM minutes 2025", "building", "Special assessment voted.", Page: 1),
    ];

    [Fact]
    public void Resolves_labels_to_retrieved_chunks_in_evidence_order()
    {
        var result = CitationValidator.Resolve(["S2", "S1"], Retrieved);

        Assert.Equal(["agm-2025", "bylaws-2024"], result.Citations.Select(c => c.DocumentId));
        Assert.Empty(result.RejectedLabels);
    }

    [Fact]
    public void Rejects_fabricated_labels()
    {
        var result = CitationValidator.Resolve(["S1", "S9", "bylaws-2024"], Retrieved);

        Assert.Single(result.Citations);
        Assert.Equal(["S9", "bylaws-2024"], result.RejectedLabels);
    }
}