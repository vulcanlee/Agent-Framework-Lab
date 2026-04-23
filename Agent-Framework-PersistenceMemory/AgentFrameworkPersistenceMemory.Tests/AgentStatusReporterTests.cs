using AgentFrameworkPersistenceMemory.Infrastructure;
using AgentFrameworkPersistenceMemory.Status;

namespace AgentFrameworkPersistenceMemory.Tests;

public class AgentStatusReporterTests
{
    [Fact]
    public void Report_WritesReadableStatusLine()
    {
        using var writer = new StringWriter();
        var reporter = new AgentStatusReporter(writer);

        reporter.Report("正在檢查目前 session 上下文");

        var output = writer.ToString();
        Assert.Contains("[Agent]", output);
        Assert.Contains("正在檢查目前 session 上下文", output);
    }

    [Fact]
    public void ReportTokenUsage_WritesStructuredUsageLine()
    {
        using var writer = new StringWriter();
        var reporter = new AgentStatusReporter(writer);

        reporter.ReportTokenUsage(new ModelUsage(12, 34, 5));

        var output = writer.ToString();
        Assert.Contains("Token 使用量：input=12, output=34, other=5", output);
    }
}
