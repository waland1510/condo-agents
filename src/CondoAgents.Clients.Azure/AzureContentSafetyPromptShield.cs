using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Azure.Core;

namespace CondoAgents.Clients.Azure;

/// <summary>Plain HTTP against the Prompt Shields endpoint; the request is three fields and the SDK adds nothing a reader needs. The service takes five documents per call, so a larger set is several calls whose indexes are re-based.</summary>
public sealed class AzureContentSafetyPromptShield(AzureOptions options, TokenCredential credential, HttpClient http) : IPromptShield
{
    private const string Scope = "https://cognitiveservices.azure.com/.default";

    private const int MaxDocumentCharacters = 10_000;

    public const int MaxDocumentsPerCall = 5;

    public async Task<PromptShieldResult> AnalyzeAsync(string userPrompt, IReadOnlyList<string> documents, CancellationToken ct = default)
    {
        var attack = false;
        var attacked = new List<int>();
        var batches = documents.Count == 0 ? [Array.Empty<string>()] : documents.Chunk(MaxDocumentsPerCall).ToList();
        for (var b = 0; b < batches.Count; b++)
        {
            var (batchAttack, batchAttacked) = await CallAsync(userPrompt, batches[b], ct);
            attack |= batchAttack;
            attacked.AddRange(batchAttacked.Select(i => b * MaxDocumentsPerCall + i));
        }

        return new PromptShieldResult(attack, attacked);
    }

    private async Task<(bool Attack, IReadOnlyList<int> Attacked)> CallAsync(string userPrompt, IReadOnlyList<string> documents, CancellationToken ct)
    {
        var token = await credential.GetTokenAsync(new TokenRequestContext([Scope]), ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{options.EffectiveContentSafetyEndpoint.TrimEnd('/')}/contentsafety/text:shieldPrompt?api-version=2024-09-01");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        request.Content = JsonContent.Create(new
        {
            userPrompt = userPrompt.Length > MaxDocumentCharacters ? userPrompt[..MaxDocumentCharacters] : userPrompt,
            documents = documents.Select(d => d.Length > MaxDocumentCharacters ? d[..MaxDocumentCharacters] : d).ToList(),
        });

        using var response = await http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Prompt Shields returned {(int)response.StatusCode}: {body}");
        }

        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        var attack = root.TryGetProperty("userPromptAnalysis", out var p) && p.GetProperty("attackDetected").GetBoolean();
        var attacked = root.GetProperty("documentsAnalysis").EnumerateArray()
            .Select((d, i) => (Index: i, Attacked: d.GetProperty("attackDetected").GetBoolean()))
            .Where(x => x.Attacked)
            .Select(x => x.Index)
            .ToList();
        return (attack, attacked);
    }
}