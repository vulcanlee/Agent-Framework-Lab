using FluentAssertions;
using NvidiaTravelAgent.Models;
using NvidiaTravelAgent.Services;

namespace NvidiaTravelAgent.Tests.Services;

public class WebSearchServiceTests
{
    [Fact]
    public void NormalizeResults_removes_blank_duplicate_and_unsupported_urls()
    {
        var input = new[]
        {
            new SearchResult("台南旅遊網", "https://www.twtainan.net/"),
            new SearchResult("重複", "https://www.twtainan.net/"),
            new SearchResult("空白", " "),
            new SearchResult("javascript", "javascript:void(0)"),
            new SearchResult("相對路徑", "/relative"),
            new SearchResult("高鐵", "https://www.thsrc.com.tw/")
        };

        var results = WebSearchService.NormalizeResults(input).ToList();

        results.Should().HaveCount(2);
        results.Select(x => x.Url).Should().BeEquivalentTo(
            "https://www.twtainan.net/",
            "https://www.thsrc.com.tw/");
    }
}
