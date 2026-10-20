namespace CondoAgents.Domain.Scoping;

/// <summary>Resolved by the caller before any agent runs; agents and tools receive it, never construct it.</summary>
public sealed record Scope(string BuildingId, bool IncludeReferenceCorpus = true);