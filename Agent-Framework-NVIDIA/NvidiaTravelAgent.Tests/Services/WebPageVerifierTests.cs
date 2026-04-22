using FluentAssertions;
using NvidiaTravelAgent.Services;

namespace NvidiaTravelAgent.Tests.Services;

public class WebPageVerifierTests
{
    [Fact]
    public async Task VerifyHtmlAsync_extracts_title_summary_and_key_facts()
    {
        const string html = """
            <html>
            <head>
              <title>赤崁樓 | 台南旅遊網</title>
              <meta name="description" content="赤崁樓是台南知名古蹟景點。" />
            </head>
            <body>
              <main>
                <p>赤崁樓位於台南市中西區，是熱門觀光景點。</p>
                <p>開放時間：08:30-21:30。</p>
                <p>地址：台南市中西區民族路二段212號。</p>
              </main>
            </body>
            </html>
            """;

        var verifier = new WebPageVerifier();

        var page = await verifier.VerifyHtmlAsync("https://www.twtainan.net/attractions/detail/123", html, CancellationToken.None);

        page.Title.Should().Be("赤崁樓 | 台南旅遊網");
        page.Summary.Should().Contain("赤崁樓");
        page.Facts.Should().Contain(x => x.Contains("開放時間"));
        page.Facts.Should().Contain(x => x.Contains("地址"));
    }
}
