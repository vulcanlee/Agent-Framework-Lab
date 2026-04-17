using System.Text.Json;
using AiTopicPulse.Cli.Sources;

namespace AiTopicPulse.Cli.Analysis;

public static class AnalysisDatasetBuilder
{
    public static AnalysisDataset Build(string topic, int windowHours, IReadOnlyList<SourceFetchResult> results)
    {
        List<SourceFetchResult> successful = results.Where(result => result.IsSuccess).ToList();
        if (successful.Count == 0)
        {
            throw new InvalidOperationException("No source data was collected. All sources failed.");
        }

        List<FailedSource> failedSources = results
            .Where(result => !result.IsSuccess)
            .Select(result => new FailedSource(result.Source, result.ErrorMessage ?? "Unknown error"))
            .ToList();

        TopicMention[] items = successful.SelectMany(result => result.Items).ToArray();

        var payload = new
        {
            topic,
            windowHours,
            generatedAtUtc = DateTimeOffset.UtcNow,
            successfulSources = successful.Select(result => result.Source).ToArray(),
            failedSources,
            items
        };

        string jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        return new AnalysisDataset(
            Topic: topic,
            WindowHours: windowHours,
            JsonPayload: jsonPayload,
            Items: items,
            SuccessfulSources: successful.Select(result => result.Source).ToArray(),
            FailedSources: failedSources);
    }
}
