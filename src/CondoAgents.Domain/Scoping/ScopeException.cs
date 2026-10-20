namespace CondoAgents.Domain.Scoping;

public sealed class ScopeException(string message) : InvalidOperationException(message);