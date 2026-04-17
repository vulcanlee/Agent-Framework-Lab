using AiTopicPulse.Cli;
using AiTopicPulse.Cli.Analysis;
using AiTopicPulse.Cli.Configuration;
using AiTopicPulse.Cli.Status;
using AiTopicPulse.Cli.Sources;

namespace AiTopicPulse.Cli.Tests.Workflow;

public sealed class AiTopicPulseWorkflowTests
{
    [Fact]
    public async Task RunAsync_reports_source_and_analysis_statuses_to_the_screen()
    {
        AppOptions options = new(
            OpenAIApiKey: "test-key",
            Model: "gpt-5-mini",
            Topic: "AI",
            WindowHours: 24);

        FakeStatusReporter reporter = new();
        FakeTrendingSource hackerNews = new(
            "Hacker News",
            SourceFetchResult.Success("Hacker News",
            [
                new TopicMention("Hacker News", "AI launch", "https://example.com/1", DateTimeOffset.UtcNow, 99, 10, "summary", "alice")
            ]));
        FakeTrendingSource reddit = new(
            "Reddit",
            SourceFetchResult.Failure("Reddit", "rate limited"));
        FakeTrendAnalyzer analyzer = new();

        AiTopicPulseWorkflow workflow = new(
            [hackerNews, reddit],
            analyzer,
            reporter);

        await workflow.RunAsync(options, CancellationToken.None);

        Assert.Collection(
            reporter.Messages,
            message => Assert.Contains("Loading configuration", message, StringComparison.Ordinal),
            message => Assert.Contains("[Hacker News Agent] Fetching", message, StringComparison.Ordinal),
            message => Assert.Contains("[Hacker News Agent] Collected 1 items", message, StringComparison.Ordinal),
            message => Assert.Contains("[Reddit Agent] Fetching", message, StringComparison.Ordinal),
            message => Assert.Contains("[Reddit Agent] Failed: rate limited", message, StringComparison.Ordinal),
            message => Assert.Contains("[Analysis Agent] Building dataset", message, StringComparison.Ordinal),
            message => Assert.Contains("[Analysis Agent] Starting analysis", message, StringComparison.Ordinal),
            message => Assert.Contains("[Analysis Agent] Analysis complete", message, StringComparison.Ordinal));
    }

    private sealed class FakeTrendingSource(string sourceName, SourceFetchResult result) : ITrendingSource
    {
        public string SourceName { get; } = sourceName;

        public Task<SourceFetchResult> FetchAsync(string topic, int windowHours, CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private sealed class FakeTrendAnalyzer : ITrendAnalyzer
    {
        public Task<PulseReport> AnalyzeAsync(
            AnalysisDataset dataset,
            IStatusReporter statusReporter,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new PulseReport(
                Markdown: "report",
                SuccessfulSources: dataset.SuccessfulSources,
                FailedSources: dataset.FailedSources,
                CodeInterpreterTranscript: null));
        }
    }

    private sealed class FakeStatusReporter : IStatusReporter
    {
        public List<string> Messages { get; } = [];

        public void Report(string message) => Messages.Add(message);
    }
}
