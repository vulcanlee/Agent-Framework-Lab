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
            new SearchResult("香港旅遊發展局", "https://www.discoverhongkong.com/"),
            new SearchResult("重複來源", "https://www.discoverhongkong.com/"),
            new SearchResult("空白", " "),
            new SearchResult("javascript", "javascript:void(0)"),
            new SearchResult("相對路徑", "/relative"),
            new SearchResult("港鐵", "https://www.mtr.com.hk/")
        };

        var results = WebSearchService.NormalizeResults(input).ToList();

        results.Should().HaveCount(2);
        results.Select(x => x.Url).Should().BeEquivalentTo(
            "https://www.discoverhongkong.com/",
            "https://www.mtr.com.hk/");
    }
}
