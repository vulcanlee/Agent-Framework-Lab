using AiTopicPulse.Cli.Analysis;
using AiTopicPulse.Cli.Configuration;
using OpenAI.Responses;

namespace AiTopicPulse.Cli.Tests.Analysis;

public sealed class OpenAiCodeInterpreterAnalyzerTests
{
    [Fact]
    public void CreateResponseOptions_enables_streaming_for_streaming_api_calls()
    {
        AppOptions appOptions = new(
            OpenAIApiKey: "test-key",
            Model: "gpt-5-mini",
            Topic: "AI",
            WindowHours: 24);
        AnalysisDataset dataset = new(
            Topic: "AI",
            WindowHours: 24,
            JsonPayload: """{"topic":"AI","windowHours":24,"items":[]}""",
            Items: [],
            SuccessfulSources: ["Hacker News"],
            FailedSources: []);

        CreateResponseOptions options = OpenAiCodeInterpreterAnalyzer.CreateResponseOptions(appOptions, dataset);

        Assert.True(options.StreamingEnabled);
        Assert.Equal("gpt-5-mini", options.Model);
        Assert.Single(options.Tools);
    }
}
