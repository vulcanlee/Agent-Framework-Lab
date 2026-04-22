using FluentAssertions;
using NvidiaTravelAgent.Models;
using NvidiaTravelAgent.Services;
using System.Net;
using System.Text;

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

    [Fact]
    public async Task SearchAsync_falls_back_to_bing_rss_when_other_providers_return_no_results()
    {
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    <html>
                      <body>
                        <form id="challenge-form">
                          <div class="anomaly-modal__title">Unfortunately, bots use DuckDuckGo too.</div>
                        </form>
                      </body>
                    </html>
                    """, Encoding.UTF8, "text/html")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    <html>
                      <body>
                        <ol id="b_results"></ol>
                      </body>
                    </html>
                    """, Encoding.UTF8, "text/html")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    <?xml version="1.0" encoding="utf-8" ?>
                    <rss version="2.0">
                      <channel>
                        <title>Bing: hong kong egg tart</title>
                        <item>
                          <title>泰昌餅家</title>
                          <link>https://www.taoheung.com.hk/tc/menu/detail/egg_tart</link>
                          <description>香港蛋塔推薦。</description>
                        </item>
                        <item>
                          <title>香港旅遊發展局</title>
                          <link>https://www.discoverhongkong.com/tc/explore/dining.html</link>
                          <description>香港美食資訊。</description>
                        </item>
                      </channel>
                    </rss>
                    """, Encoding.UTF8, "application/rss+xml")
            });

        var service = new WebSearchService(new HttpClient(handler));

        var results = await service.SearchAsync("香港 蛋塔 推薦");

        results.Should().HaveCount(2);
        results[0].Title.Should().Be("泰昌餅家");
        results.Select(result => result.Url).Should().Contain("https://www.discoverhongkong.com/tc/explore/dining.html");
    }

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public SequenceHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No more mocked responses.");
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }
}
