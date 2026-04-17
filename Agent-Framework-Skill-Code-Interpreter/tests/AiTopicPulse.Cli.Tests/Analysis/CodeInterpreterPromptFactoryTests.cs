using AiTopicPulse.Cli.Analysis;

namespace AiTopicPulse.Cli.Tests.Analysis;

public sealed class CodeInterpreterPromptFactoryTests
{
    [Fact]
    public void Create_mentions_code_interpreter_responsibilities_explicitly()
    {
        AnalysisDataset dataset = new(
            Topic: "AI",
            WindowHours: 24,
            JsonPayload: """{"topic":"AI","windowHours":24,"items":[]}""",
            Items: [],
            SuccessfulSources: ["Hacker News", "GitHub"],
            FailedSources: []);

        string prompt = CodeInterpreterPromptFactory.Create(dataset);

        Assert.Contains("Use the code interpreter", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("deduplicate", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("top 10", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("JSON dataset", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Hacker News, GitHub", prompt, StringComparison.Ordinal);
    }
}
