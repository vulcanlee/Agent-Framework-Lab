namespace AiTopicPulse.Cli.Sources;

public sealed record SourceFetchResult(
    string Source,
    IReadOnlyList<TopicMention> Items,
    bool IsSuccess,
    string? ErrorMessage)
{
    public static SourceFetchResult Success(string source, IReadOnlyList<TopicMention> items) =>
        new(source, items, true, null);

    public static SourceFetchResult Failure(string source, string errorMessage) =>
        new(source, [], false, errorMessage);
}
