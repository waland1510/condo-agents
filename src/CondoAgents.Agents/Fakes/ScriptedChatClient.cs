using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.Extensions.AI;

namespace CondoAgents.Agents.Fakes;

/// <summary>A deterministic model: searches with the user's words, then answers from the passages it got back. Scripts override either step so tests can make it misbehave on purpose.</summary>
public sealed partial class ScriptedChatClient(ScriptedChatClient.Script? script = null) : IChatClient
{
    public sealed record Script(
        Func<string, IReadOnlyList<AITool>, FunctionCallContent?>? FirstCall = null,
        Func<string, IReadOnlyList<ToolResult>, string>? FinalJson = null);

    public sealed record ToolResult(string Name, string Json);

    public ChatClientMetadata Metadata { get; } = new("scripted-fake", null, "fake-1");

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var list = messages.ToList();
        var question = list.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? string.Empty;
        var toolResults = list
            .SelectMany(m => m.Contents.OfType<FunctionResultContent>())
            .Select(r => new ToolResult(CallName(list, r.CallId), r.Result?.ToString() ?? "null"))
            .ToList();
        IReadOnlyList<AITool> tools = options?.Tools is { } t ? [.. t] : [];

        ChatMessage reply;
        if (toolResults.Count == 0 && tools.Count > 0)
        {
            var call = script?.FirstCall is not null ? script.FirstCall(question, tools) : DefaultFirstCall(question, tools);
            reply = call is null ? new ChatMessage(ChatRole.Assistant, AnswerJson(question, toolResults)) : new ChatMessage(ChatRole.Assistant, [call]);
        }
        else
        {
            var json = script?.FinalJson is not null ? script.FinalJson(question, toolResults) : AnswerJson(question, toolResults);
            reply = new ChatMessage(ChatRole.Assistant, json);
        }

        var inputTokens = list.Sum(m => m.Text.Length) / 4;
        var outputTokens = reply.Text.Length / 4;
        return Task.FromResult(new ChatResponse(reply)
        {
            ModelId = "fake-1",
            FinishReason = reply.Contents.OfType<FunctionCallContent>().Any() ? ChatFinishReason.ToolCalls : ChatFinishReason.Stop,
            Usage = new UsageDetails { InputTokenCount = inputTokens, OutputTokenCount = outputTokens, TotalTokenCount = inputTokens + outputTokens },
        });
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (var update in response.ToChatResponseUpdates())
        {
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
    }

    private static FunctionCallContent? DefaultFirstCall(string question, IReadOnlyList<AITool> tools)
    {
        var search = tools.OfType<AIFunction>().FirstOrDefault(t => t.Name is "search_documents" or "civil_code_lookup");
        if (search is null)
        {
            return null;
        }

        var argument = search.Name == "search_documents" ? "query" : "topic";
        return new FunctionCallContent("call-1", search.Name, new Dictionary<string, object?> { [argument] = Keywords(question) });
    }

    /// <summary>Keeps the content words so the keyword retriever has something to match on; a real model would write the query itself.</summary>
    public static string Keywords(string question)
    {
        var words = Words().Matches(question).Select(m => m.Value.ToLowerInvariant()).Where(w => w.Length > 3 && !StopWords.Contains(w));
        return string.Join(' ', words);
    }

    private static string AnswerJson(string question, IReadOnlyList<ToolResult> toolResults)
    {
        var passages = toolResults
            .SelectMany(r => ParsePassages(r.Json))
            .Take(3)
            .ToList();

        if (passages.Count == 0)
        {
            return JsonSerializer.Serialize(new { kind = "answer", text = "The documents do not answer this question.", evidence = Array.Empty<string>() });
        }

        var text = string.Join(" ", passages.Select(p => $"{FirstSentence(p.Text)} [{p.Label}]"));
        return JsonSerializer.Serialize(new { kind = "answer", text, evidence = passages.Select(p => p.Label).ToArray() });
    }

    public static IReadOnlyList<(string Label, string Text)> ParsePassages(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return doc.RootElement.EnumerateArray()
                .Where(e => e.TryGetProperty("label", out _) && e.TryGetProperty("text", out _))
                .Select(e => (e.GetProperty("label").GetString()!, e.GetProperty("text").GetString()!))
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string FirstSentence(string text)
    {
        var flat = text.Replace('\n', ' ');
        var end = flat.IndexOfAny(['.', ';']);
        return end > 0 && end < 220 ? flat[..(end + 1)] : flat[..Math.Min(flat.Length, 220)];
    }

    private static string CallName(List<ChatMessage> messages, string callId)
        => messages.SelectMany(m => m.Contents.OfType<FunctionCallContent>()).FirstOrDefault(c => c.CallId == callId)?.Name ?? "unknown";

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "what", "does", "this", "that", "with", "from", "have", "there", "about", "which", "when", "will", "been", "were", "their", "condo", "building",
    };

    [GeneratedRegex(@"[\p{L}\d-]+")]
    private static partial Regex Words();
}