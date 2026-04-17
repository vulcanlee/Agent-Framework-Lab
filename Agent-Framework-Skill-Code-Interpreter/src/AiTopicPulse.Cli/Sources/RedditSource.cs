using System.Globalization;
using System.Text.Json;

namespace AiTopicPulse.Cli.Sources;

public sealed class RedditSource(HttpClient httpClient) : ITrendingSource
{
    private readonly HttpClient _httpClient = httpClient;

    public string SourceName => "Reddit";

    public async Task<SourceFetchResult> FetchAsync(string topic, int windowHours, CancellationToken cancellationToken)
    {
        try
        {
            DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddHours(-windowHours);
            string url =
                $"https://www.reddit.com/search.json?q={Uri.EscapeDataString(topic)}&sort=top&t=day&limit=10";

            using HttpRequestMessage request = new(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("AiTopicPulse/1.0");

            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            List<TopicMention> items = document.RootElement
                .GetProperty("data")
                .GetProperty("children")
                .EnumerateArray()
                .Select(child => child.GetProperty("data"))
                .Select(item =>
                {
                    DateTimeOffset publishedAt = DateTimeOffset.FromUnixTimeSeconds(item.GetProperty("created_utc").GetInt64());
                    string summary = item.TryGetProperty("selftext", out JsonElement selfTextElement) ? selfTextElement.GetString() ?? string.Empty : string.Empty;
                    return new TopicMention(
                        Source: SourceName,
                        Title: item.GetProperty("title").GetString() ?? "(untitled)",
                        Url: $"https://www.reddit.com{item.GetProperty("permalink").GetString()}",
                        PublishedAtUtc: publishedAt,
                        Score: item.GetProperty("score").GetDouble(),
                        CommentCount: item.GetProperty("num_comments").GetInt32(),
                        Summary: summary.Length > 280 ? summary[..280] + "..." : summary,
                        Author: item.GetProperty("author").GetString() ?? "unknown");
                })
                .Where(item => item.PublishedAtUtc >= cutoff)
                .Take(10)
                .ToList();

            return SourceFetchResult.Success(SourceName, items);
        }
        catch (Exception exception)
        {
            return SourceFetchResult.Failure(SourceName, exception.Message);
        }
    }
}
