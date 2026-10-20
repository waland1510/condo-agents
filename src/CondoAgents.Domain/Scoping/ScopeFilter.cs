using System.Text.RegularExpressions;

namespace CondoAgents.Domain.Scoping;

/// <summary>The retrieval access boundary. Built in code per request and passed as the search filter; a malformed scope throws instead of defaulting open.</summary>
public static partial class ScopeFilter
{
    public const string BuildingField = "building_id";
    public const string CorpusField = "corpus";
    public const string ActiveField = "is_active";
    public const string ReferenceCorpus = "reference";
    public const string BuildingCorpus = "building";

    public static string Build(Scope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (!BuildingIdPattern().IsMatch(scope.BuildingId))
        {
            throw new ScopeException("Scope has no valid building id; refusing to build an open filter.");
        }

        var building = $"({BuildingField} eq '{scope.BuildingId}' and {CorpusField} eq '{BuildingCorpus}' and {ActiveField} eq true)";
        if (!scope.IncludeReferenceCorpus)
        {
            return building;
        }

        return $"{building} or ({CorpusField} eq '{ReferenceCorpus}' and {ActiveField} eq true)";
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,62}[a-z0-9]$|^[a-z0-9]$")]
    private static partial Regex BuildingIdPattern();
}