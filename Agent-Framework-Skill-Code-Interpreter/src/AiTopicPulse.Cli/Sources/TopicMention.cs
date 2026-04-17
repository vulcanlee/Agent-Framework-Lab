namespace AiTopicPulse.Cli.Sources;

public sealed record TopicMention(
    string Source,
    string Title,
    string Url,
    DateTimeOffset PublishedAtUtc,
    double Score,
    int CommentCount,
    string Summary,
    string Author);
