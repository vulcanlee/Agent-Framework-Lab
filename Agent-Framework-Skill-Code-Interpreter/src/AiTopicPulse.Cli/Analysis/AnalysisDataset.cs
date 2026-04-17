namespace AiTopicPulse.Cli.Analysis;

public sealed record AnalysisDataset(
    string Topic,
    int WindowHours,
    string JsonPayload,
    IReadOnlyList<Sources.TopicMention> Items,
    IReadOnlyList<string> SuccessfulSources,
    IReadOnlyList<FailedSource> FailedSources);

public sealed record FailedSource(string Source, string ErrorMessage);
