using AiTopicPulse.Cli.Status;

namespace AiTopicPulse.Cli.Analysis;

public interface ITrendAnalyzer
{
    Task<PulseReport> AnalyzeAsync(
        AnalysisDataset dataset,
        IStatusReporter statusReporter,
        CancellationToken cancellationToken);
}
