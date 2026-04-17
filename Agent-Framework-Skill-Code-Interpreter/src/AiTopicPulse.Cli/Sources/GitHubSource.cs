using System.Globalization;
using System.Text.Json;

namespace AiTopicPulse.Cli.Sources;

public sealed class GitHubSource(HttpClient httpClient) : ITrendingSource
{
    private readonly HttpClient _httpClient = httpClient;

    public string SourceName => "GitHub";

    public async Task<SourceFetchResult> FetchAsync(string topic, int windowHours, CancellationToken cancellationToken)
    {
        try
        {
            DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddHours(-windowHours);
            string since = cutoff.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
            string url =
                $"https://api.github.com/search/issues?q={Uri.EscapeDataString(topic)}+created:%3E{Uri.EscapeDataString(since)}&sort=comments&order=desc&per_page=10";

            using HttpRequestMessage request = new(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("AiTopicPulse/1.0");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            List<TopicMention> items = document.RootElement.GetProperty("items")
                .EnumerateArray()
                .Select(item =>
                {
                    DateTimeOffset publishedAt = DateTimeOffset.Parse(item.GetProperty("created_at").GetString()!, CultureInfo.InvariantCulture);
                    string body = item.TryGetProperty("body", out JsonElement bodyElement) ? bodyElement.GetString() ?? string.Empty : string.Empty;
                    return new TopicMention(
                        Source: SourceName,
                        Title: item.GetProperty("title").GetString() ?? "(untitled)",
                        Url: item.GetProperty("html_url").GetString() ?? string.Empty,
                        PublishedAtUtc: publishedAt,
                        Score: item.TryGetProperty("comments", out JsonElement commentsElement) ? commentsElement.GetInt32() : 0,
                        CommentCount: item.TryGetProperty("comments", out JsonElement commentsCountElement) ? commentsCountElement.GetInt32() : 0,
                        Summary: body.Length > 280 ? body[..280] + "..." : body,
                        Author: item.GetProperty("user").GetProperty("login").GetString() ?? "unknown");
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
