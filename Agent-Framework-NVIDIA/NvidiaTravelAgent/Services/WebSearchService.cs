using AngleSharp.Html.Parser;
using NvidiaTravelAgent.Models;
using System.Web;

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
        var url = $"https://html.duckduckgo.com/html/?q={HttpUtility.UrlEncode(query)}";
        var html = await _httpClient.GetStringAsync(url, cancellationToken);
        var document = await _parser.ParseDocumentAsync(html, cancellationToken);

        var results = document.QuerySelectorAll("a.result__a")
            .Select(node => new SearchResult(
                node.TextContent.Trim(),
                node.GetAttribute("href") ?? string.Empty));

        return NormalizeResults(results).Take(5).ToList();
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
}
