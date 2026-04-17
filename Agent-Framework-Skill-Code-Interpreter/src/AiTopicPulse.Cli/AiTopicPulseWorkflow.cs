using AiTopicPulse.Cli.Analysis;
using AiTopicPulse.Cli.Configuration;
using AiTopicPulse.Cli.Status;
using AiTopicPulse.Cli.Sources;

namespace AiTopicPulse.Cli;

public sealed class AiTopicPulseWorkflow(
    IReadOnlyList<ITrendingSource> sources,
    ITrendAnalyzer analyzer,
    IStatusReporter statusReporter)
{
    private readonly IReadOnlyList<ITrendingSource> _sources = sources;
    private readonly ITrendAnalyzer _analyzer = analyzer;
    private readonly IStatusReporter _statusReporter = statusReporter;

    public async Task<PulseReport> RunAsync(AppOptions options, CancellationToken cancellationToken)
    {
        _statusReporter.Report("Loading configuration.");

        List<SourceFetchResult> results = [];
        foreach (ITrendingSource source in _sources)
        {
            _statusReporter.Report($"[{source.SourceName} Agent] Fetching recent {options.Topic} discussions.");
            SourceFetchResult result = await source.FetchAsync(options.Topic, options.WindowHours, cancellationToken);
            results.Add(result);

            if (result.IsSuccess)
            {
                _statusReporter.Report($"[{source.SourceName} Agent] Collected {result.Items.Count} items.");
            }
            else
            {
                _statusReporter.Report($"[{source.SourceName} Agent] Failed: {result.ErrorMessage}");
            }
        }

        _statusReporter.Report("[Analysis Agent] Building dataset.");
        AnalysisDataset dataset = AnalysisDatasetBuilder.Build(options.Topic, options.WindowHours, results);

        _statusReporter.Report("[Analysis Agent] Starting analysis.");
        PulseReport report = await _analyzer.AnalyzeAsync(dataset, _statusReporter, cancellationToken);
        _statusReporter.Report("[Analysis Agent] Analysis complete.");
        return report;
    }
}
