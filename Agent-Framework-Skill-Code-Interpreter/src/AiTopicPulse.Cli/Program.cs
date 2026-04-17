using AiTopicPulse.Cli.Analysis;
using AiTopicPulse.Cli.Configuration;
using AiTopicPulse.Cli.Output;
using AiTopicPulse.Cli.Status;
using AiTopicPulse.Cli.Sources;

namespace AiTopicPulse.Cli;

public sealed class Program
{
    public static async Task Main(string[] args)
    {
        AppOptions options = AppOptionsLoader.Load();
        using HttpClient httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        IReadOnlyList<ITrendingSource> sources =
        [
            new HackerNewsSource(httpClient),
            new GitHubSource(httpClient),
            new RedditSource(httpClient)
        ];

        ITrendAnalyzer analyzer = new OpenAiCodeInterpreterAnalyzer(options);
        IStatusReporter statusReporter = new ConsoleStatusReporter();
        AiTopicPulseWorkflow workflow = new(sources, analyzer, statusReporter);
        PulseReport report = await workflow.RunAsync(options, CancellationToken.None);
        ConsoleReportWriter.Write(report);
    }
}
