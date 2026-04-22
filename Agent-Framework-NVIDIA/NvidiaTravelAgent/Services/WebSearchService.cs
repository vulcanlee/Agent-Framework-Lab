using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using NvidiaTravelAgent.Models;
using System.Web;
using System.Xml.Linq;

namespace NvidiaTravelAgent.Services;

public sealed class WebSearchService : IWebSearchService
{
    private readonly HttpClient _httpClient;
    private readonly HtmlParser _parser = new();

    public WebSearchService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var results = await SearchDuckDuckGoAsync(query, cancellationToken);
        if (results.Count > 0)
        {
            return results;
        }

        results = await SearchBingHtmlAsync(query, cancellationToken);
        if (results.Count > 0)
        {
            return results;
        }

        results = await SearchBingRssAsync(query, cancellationToken);
        return results;
    }

    public static IEnumerable<SearchResult> NormalizeResults(IEnumerable<SearchResult> input)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var result in input)
        {
            if (string.IsNullOrWhiteSpace(result.Url))
            {
                continue;
            }

            if (!Uri.TryCreate(result.Url, UriKind.Absolute, out var uri))
            {
                continue;
            }

            if (uri.Scheme is not ("http" or "https"))
            {
                continue;
            }

            var normalized = uri.ToString();
            if (!seen.Add(normalized))
            {
                continue;
            }

            yield return result with { Url = normalized };
        }
    }

    private async Task<IReadOnlyList<SearchResult>> SearchDuckDuckGoAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"https://html.duckduckgo.com/html/?q={HttpUtility.UrlEncode(query)}";
            var html = await _httpClient.GetStringAsync(url, cancellationToken);
            if (LooksLikeDuckDuckGoBotChallenge(html))
            {
                return [];
            }

            var document = await _parser.ParseDocumentAsync(html, cancellationToken);
            var results = document.QuerySelectorAll("a.result__a, a.result-link")
                .Select(node => new SearchResult(
                    node.TextContent.Trim(),
                    node.GetAttribute("href") ?? string.Empty));

            return NormalizeResults(results).Take(8).ToList();
        }
        catch
        {
            return [];
        }
    }

    private async Task<IReadOnlyList<SearchResult>> SearchBingHtmlAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"https://www.bing.com/search?q={HttpUtility.UrlEncode(query)}";
            var html = await _httpClient.GetStringAsync(url, cancellationToken);
            var document = await _parser.ParseDocumentAsync(html, cancellationToken);

            var results = document.QuerySelectorAll("li.b_algo h2 a")
                .Select(node => new SearchResult(
                    node.TextContent.Trim(),
                    ResolveBingUrl(node)))
                .Where(result => !string.IsNullOrWhiteSpace(result.Title));

            return NormalizeResults(results).Take(8).ToList();
        }
        catch
        {
            return [];
        }
    }

    private async Task<IReadOnlyList<SearchResult>> SearchBingRssAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"https://www.bing.com/search?format=rss&q={HttpUtility.UrlEncode(query)}";
            var xml = await _httpClient.GetStringAsync(url, cancellationToken);
            var document = XDocument.Parse(xml);

            var results = document.Descendants("item")
                .Select(item => new SearchResult(
                    item.Element("title")?.Value.Trim() ?? string.Empty,
                    item.Element("link")?.Value.Trim() ?? string.Empty));

            return NormalizeResults(results).Take(8).ToList();
        }
        catch
        {
            return [];
        }
    }

    private static bool LooksLikeDuckDuckGoBotChallenge(string html)
    {
        return html.Contains("anomaly-modal", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("Unfortunately, bots use DuckDuckGo too", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("challenge-form", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveBingUrl(IElement node)
    {
        var href = node.GetAttribute("href") ?? string.Empty;
        if (!Uri.TryCreate(href, UriKind.Absolute, out var uri))
        {
            return href;
        }

        if (!uri.Host.Contains("bing.com", StringComparison.OrdinalIgnoreCase))
        {
            return href;
        }

        var u = HttpUtility.ParseQueryString(uri.Query)["u"];
        if (string.IsNullOrWhiteSpace(u))
        {
            return href;
        }

        if (TryDecodeBingWrappedUrl(u, out var decoded))
        {
            return decoded;
        }

        return href;
    }

    private static bool TryDecodeBingWrappedUrl(string value, out string url)
    {
        url = string.Empty;
        if (!value.StartsWith("a1", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = value[2..];
        payload = payload.Replace('-', '+').Replace('_', '/');
        while (payload.Length % 4 != 0)
        {
            payload += "=";
        }

        try
        {
            var bytes = Convert.FromBase64String(payload);
            var decoded = System.Text.Encoding.UTF8.GetString(bytes);
            if (!Uri.TryCreate(decoded, UriKind.Absolute, out _))
            {
                return false;
            }

            url = decoded;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
