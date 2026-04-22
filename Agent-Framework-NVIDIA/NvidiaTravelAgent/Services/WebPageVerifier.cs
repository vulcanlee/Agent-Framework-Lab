using AngleSharp.Html.Parser;
using NvidiaTravelAgent.Models;
using System.Text.RegularExpressions;

namespace NvidiaTravelAgent.Services;

public sealed class WebPageVerifier : IWebPageVerifier
{
    private static readonly Regex FactRegex = new(
        "(營業|地址|交通|住宿|票價|開放|分店|菜單|推薦|外帶)",
        RegexOptions.Compiled);

    private readonly HttpClient? _httpClient;
    private readonly HtmlParser _parser = new();

    public WebPageVerifier()
    {
    }

    public WebPageVerifier(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<VerifiedSource> VerifyAsync(string url, CancellationToken cancellationToken = default)
    {
        if (_httpClient is null)
        {
            throw new InvalidOperationException("尚未提供 HttpClient，無法抓取遠端網頁。");
        }

        var html = await _httpClient.GetStringAsync(url, cancellationToken);
        return await VerifyHtmlAsync(url, html, cancellationToken);
    }

    public async Task<VerifiedSource> VerifyHtmlAsync(string url, string html, CancellationToken cancellationToken = default)
    {
        var document = await _parser.ParseDocumentAsync(html, cancellationToken);
        var title = document.Title?.Trim() ?? string.Empty;
        var summary = document.QuerySelector("meta[name=description]")?.GetAttribute("content")?.Trim();

        var paragraphs = document.QuerySelectorAll("p")
            .Select(node => node.TextContent.Trim())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();

        var facts = paragraphs
            .Where(text => FactRegex.IsMatch(text))
            .Distinct(StringComparer.Ordinal)
            .Take(8)
            .ToList();

        if (facts.Count == 0)
        {
            facts = paragraphs.Take(3).ToList();
        }

        return new VerifiedSource
        {
            Url = url,
            Title = title,
            Summary = !string.IsNullOrWhiteSpace(summary) ? summary : paragraphs.FirstOrDefault() ?? string.Empty,
            Facts = facts
        };
    }
}
