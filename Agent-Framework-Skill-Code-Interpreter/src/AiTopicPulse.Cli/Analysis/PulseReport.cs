namespace AiTopicPulse.Cli.Analysis;

public sealed record PulseReport(
    string Markdown,
    IReadOnlyList<string> SuccessfulSources,
    IReadOnlyList<FailedSource> FailedSources,
    string? CodeInterpreterTranscript);
