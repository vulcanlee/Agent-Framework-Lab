namespace NvidiaTravelAgent.Models;

public sealed class VerifiedSource
{
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<string> Facts { get; set; } = [];
}

public sealed class VerifiedFact
{
    public string Category { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public string SourceUrl { get; init; } = string.Empty;
    public string SourceTitle { get; init; } = string.Empty;
}
