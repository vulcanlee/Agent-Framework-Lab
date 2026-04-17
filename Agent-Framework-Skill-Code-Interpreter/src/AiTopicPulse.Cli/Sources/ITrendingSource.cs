namespace AiTopicPulse.Cli.Sources;

public interface ITrendingSource
{
    string SourceName { get; }

    Task<SourceFetchResult> FetchAsync(string topic, int windowHours, CancellationToken cancellationToken);
}
