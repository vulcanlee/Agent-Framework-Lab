using System.Text.Json;
using AiTopicPulse.Cli.Analysis;
using AiTopicPulse.Cli.Sources;

namespace AiTopicPulse.Cli.Tests.Analysis;

public sealed class AnalysisDatasetBuilderTests
{
    [Fact]
    public void Build_creates_a_json_payload_with_only_successful_sources()
    {
        TopicMention mention = new(
            Source: "Hacker News",
            Title: "New AI agent framework release",
            Url: "https://news.ycombinator.com/item?id=1",
            PublishedAtUtc: new DateTimeOffset(2026, 4, 17, 8, 0, 0, TimeSpan.Zero),
            Score: 187,
            CommentCount: 42,
            Summary: "Launch discussion for a new AI agent framework.",
            Author: "alice");

        SourceFetchResult success = SourceFetchResult.Success("Hacker News", [mention]);
        SourceFetchResult failure = SourceFetchResult.Failure("Reddit", "rate limited");

        AnalysisDataset dataset = AnalysisDatasetBuilder.Build("AI", 24, [success, failure]);

        Assert.Single(dataset.SuccessfulSources);
        Assert.Single(dataset.FailedSources);
        Assert.Single(dataset.Items);

        using JsonDocument document = JsonDocument.Parse(dataset.JsonPayload);
        JsonElement root = document.RootElement;

        Assert.Equal("AI", root.GetProperty("topic").GetString());
        Assert.Equal(24, root.GetProperty("windowHours").GetInt32());
        Assert.Single(root.GetProperty("items").EnumerateArray());
        Assert.Equal("Reddit", root.GetProperty("failedSources")[0].GetProperty("source").GetString());
    }

    [Fact]
    public void Build_throws_when_every_source_failed()
    {
        SourceFetchResult failureA = SourceFetchResult.Failure("Hacker News", "down");
        SourceFetchResult failureB = SourceFetchResult.Failure("GitHub", "timeout");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => AnalysisDatasetBuilder.Build("AI", 24, [failureA, failureB]));

        Assert.Contains("No source data", exception.Message, StringComparison.Ordinal);
    }
}
