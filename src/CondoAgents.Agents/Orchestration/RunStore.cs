using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CondoAgents.Agents.Orchestration;

public interface IRunStore
{
    Task SaveAsync(AgentRun run, CancellationToken ct = default);

    Task<AgentRun?> LoadAsync(string runId, CancellationToken ct = default);

    Task<IReadOnlyList<AgentRun>> ListAsync(CancellationToken ct = default);
}

public static class RunJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
    };
}

public sealed class InMemoryRunStore : IRunStore
{
    private readonly ConcurrentDictionary<string, string> _runs = new(StringComparer.Ordinal);

    public Task SaveAsync(AgentRun run, CancellationToken ct = default)
    {
        _runs[run.Id] = JsonSerializer.Serialize(run, RunJson.Options);
        return Task.CompletedTask;
    }

    public Task<AgentRun?> LoadAsync(string runId, CancellationToken ct = default)
        => Task.FromResult(_runs.TryGetValue(runId, out var json) ? JsonSerializer.Deserialize<AgentRun>(json, RunJson.Options) : null);

    public Task<IReadOnlyList<AgentRun>> ListAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AgentRun>>(_runs.Values.Select(j => JsonSerializer.Deserialize<AgentRun>(j, RunJson.Options)!).OrderByDescending(r => r.CreatedAt).ToList());
}

/// <summary>One JSON file per run, written atomically; enough durability to survive a process restart mid-approval.</summary>
public sealed class FileRunStore(string directory) : IRunStore
{
    public async Task SaveAsync(AgentRun run, CancellationToken ct = default)
    {
        Directory.CreateDirectory(directory);
        var path = PathFor(run.Id);
        var temp = path + ".tmp";
        await using (var stream = File.Create(temp))
        {
            await JsonSerializer.SerializeAsync(stream, run, RunJson.Options, ct);
        }

        File.Move(temp, path, overwrite: true);
    }

    public async Task<AgentRun?> LoadAsync(string runId, CancellationToken ct = default)
    {
        var path = PathFor(runId);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<AgentRun>(stream, RunJson.Options, ct);
    }

    public async Task<IReadOnlyList<AgentRun>> ListAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var runs = new List<AgentRun>();
        foreach (var file in Directory.EnumerateFiles(directory, "run-*.json"))
        {
            await using var stream = File.OpenRead(file);
            var run = await JsonSerializer.DeserializeAsync<AgentRun>(stream, RunJson.Options, ct);
            if (run is not null)
            {
                runs.Add(run);
            }
        }

        return runs.OrderByDescending(r => r.CreatedAt).ToList();
    }

    private string PathFor(string runId)
    {
        if (runId.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
        {
            throw new ArgumentException("Run id contains characters that are not allowed in a file name.", nameof(runId));
        }

        return Path.Combine(directory, runId + ".json");
    }
}