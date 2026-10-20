using Azure.AI.TextAnalytics;
using Azure.Core;

namespace CondoAgents.Clients.Azure;

/// <summary>Person-identifying categories only: Organization and the like would redact the syndicate, the insurer and the engineer's firm, which is the content a buyer needs.</summary>
public sealed class AzureLanguagePiiDetector(AzureOptions options, TokenCredential credential) : IPiiDetector
{
    private static readonly IReadOnlyList<PiiEntityCategory> Categories = [PiiEntityCategory.Person, PiiEntityCategory.Email, PiiEntityCategory.PhoneNumber, PiiEntityCategory.Address];

    private const int MaxCharacters = 5_000;

    private readonly TextAnalyticsClient _client = new(new Uri(options.LanguageEndpoint), credential);

    public async Task<IReadOnlyList<PiiFinding>> DetectAsync(string text, CancellationToken ct = default)
    {
        var findings = new List<PiiFinding>();
        for (var start = 0; start < text.Length; start += MaxCharacters)
        {
            var slice = text.Substring(start, Math.Min(MaxCharacters, text.Length - start));
            var requestOptions = new RecognizePiiEntitiesOptions();
            foreach (var category in Categories)
            {
                requestOptions.CategoriesFilter.Add(category);
            }

            var response = await _client.RecognizePiiEntitiesAsync(slice, language: null, requestOptions, ct);
            foreach (var entity in response.Value)
            {
                findings.Add(new PiiFinding(entity.Category.ToString(), entity.Text, start + entity.Offset, entity.Length, entity.ConfidenceScore));
            }
        }

        return findings;
    }
}