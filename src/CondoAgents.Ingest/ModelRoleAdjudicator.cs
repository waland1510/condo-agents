using System.Text.Json;
using System.Text.Json.Serialization;

using CondoAgents.Clients;

using Microsoft.Extensions.AI;

namespace CondoAgents.Ingest;

/// <summary>One strict-JSON call per batch; any failure or unknown id yields no verdict, which the pipeline reads as redact.</summary>
public sealed class ModelRoleAdjudicator(IChatClient model) : IRoleAdjudicator
{
    private const int BatchSize = 25;

    private const string Instructions = """
        You classify people mentioned in condominium documents by their role, from the surrounding context only.
        Roles: "owner" (a co-owner, resident, tenant, litigant or estate; anyone acting as a private individual),
        "professional" (property manager, engineer, lawyer, notary, auditor, insurer staff, contractor),
        "director" (a board member acting as such: president, treasurer, secretary, mover or seconder of a motion),
        "unknown" when the context does not say.
        Emails and phone numbers take the role of the person they belong to.
        Respond with JSON: {"verdicts":[{"id":"...","role":"owner|professional|director|unknown"}]}.
        """;

    private sealed record Verdicts([property: JsonPropertyName("verdicts")] IReadOnlyList<Verdict> Items);

    private sealed record Verdict([property: JsonPropertyName("id")] string Id, [property: JsonPropertyName("role")] string Role);

    public async Task<IReadOnlyList<RoleVerdict>> AdjudicateAsync(IReadOnlyList<RoleCandidate> candidates, CancellationToken ct = default)
    {
        var results = new List<RoleVerdict>();
        foreach (var batch in candidates.Chunk(BatchSize))
        {
            var payload = JsonSerializer.Serialize(batch.Select(c => new { c.Id, c.Category, c.Text, c.Context }));
            var response = await model.GetResponseAsync<Verdicts>(
                [new ChatMessage(ChatRole.System, Instructions), new ChatMessage(ChatRole.User, payload)],
                cancellationToken: ct);
            if (!response.TryGetResult(out var verdicts))
            {
                continue;
            }

            var ids = batch.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
            results.AddRange(verdicts.Items
                .Where(v => ids.Contains(v.Id))
                .Select(v => new RoleVerdict(v.Id, v.Role is RoleVerdict.Owner or RoleVerdict.Professional or RoleVerdict.Director ? v.Role : RoleVerdict.Unknown)));
        }

        return results;
    }
}