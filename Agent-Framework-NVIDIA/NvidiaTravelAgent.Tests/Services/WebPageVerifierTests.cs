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
              <title>泰昌餅家 | 香港蛋塔名店</title>
              <meta name="description" content="泰昌餅家以蛋塔聞名，是香港常見的外帶點心店。" />
            </head>
            <body>
              <main>
                <p>泰昌餅家的蛋塔是香港常見的外帶甜點選項。</p>
                <p>營業時間為每日 08:30-21:30。</p>
                <p>地址位於中環，方便搭配港島美食動線安排。</p>
              </main>
            </body>
            </html>
            """;

        var verifier = new WebPageVerifier();

        var page = await verifier.VerifyHtmlAsync("https://example.com/hk-egg-tart", html, CancellationToken.None);

        page.Title.Should().Be("泰昌餅家 | 香港蛋塔名店");
        page.Summary.Should().Contain("蛋塔");
        page.Facts.Should().Contain(x => x.Contains("營業時間"));
        page.Facts.Should().Contain(x => x.Contains("地址"));
    }
}
