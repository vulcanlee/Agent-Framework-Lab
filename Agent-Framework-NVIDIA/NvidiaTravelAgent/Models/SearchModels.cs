namespace NvidiaTravelAgent.Models;

public sealed record SearchResult(string Title, string Url);

public sealed class SearchQueryPlan
{
    public List<string> Queries { get; init; } = [];
}
