using System.Globalization;
using System.Text.Json;

namespace AiTopicPulse.Cli.Sources;

public sealed class HackerNewsSource(HttpClient httpClient) : ITrendingSource
{
    private readonly HttpClient _httpClient = httpClient;

    public string SourceName => "Hacker News";

    public async Task<SourceFetchResult> FetchAsync(string topic, int windowHours, CancellationToken cancellationToken)
    {
        try
        {
            DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddHours(-windowHours);
            string url =
                $"https://hn.algolia.com/api/v1/search_by_date?query={Uri.EscapeDataString(topic)}&tags=story&hitsPerPage=20";

            using HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            List<TopicMention> items = document.RootElement.GetProperty("hits")
                .EnumerateArray()
                .Select(hit =>
                {
                    string createdAt = hit.GetProperty("created_at").GetString() ?? string.Empty;
                    DateTimeOffset publishedAt = DateTimeOffset.Parse(createdAt, CultureInfo.InvariantCulture);
                    return new TopicMention(
                        Source: SourceName,
                        Title: hit.TryGetProperty("title", out JsonElement titleElement) ? titleElement.GetString() ?? "(untitled)" : "(untitled)",
                        Url: hit.TryGetProperty("url", out JsonElement urlElement) ? urlElement.GetString() ?? string.Empty : string.Empty,
                        PublishedAtUtc: publishedAt,
                        Score: hit.TryGetProperty("points", out JsonElement pointsElement) ? pointsElement.GetDouble() : 0,
                        CommentCount: hit.TryGetProperty("num_comments", out JsonElement commentsElement) ? commentsElement.GetInt32() : 0,
                        Summary: $"HN score {hit.GetProperty("points").GetDouble():0} / comments {hit.GetProperty("num_comments").GetInt32()}",
                        Author: hit.TryGetProperty("author", out JsonElement authorElement) ? authorElement.GetString() ?? "unknown" : "unknown");
                })
                .Where(item => item.PublishedAtUtc >= cutoff)
                .Where(item => !string.IsNullOrWhiteSpace(item.Url))
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
