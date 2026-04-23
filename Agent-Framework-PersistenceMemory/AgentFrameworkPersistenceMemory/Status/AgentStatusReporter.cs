using System.Globalization;
using AgentFrameworkPersistenceMemory.Infrastructure;

namespace AgentFrameworkPersistenceMemory.Status;

public sealed class AgentStatusReporter(TextWriter writer)
{
    private readonly TextWriter _writer = writer;

    public void Report(string message)
    {
        var timestamp = DateTimeOffset.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        _writer.WriteLine($"[Agent] {timestamp} {message}");
    }

    public void ReportTokenUsage(ModelUsage usage)
    {
        Report($"Token 使用量：input={usage.InputTokens}, output={usage.OutputTokens}, other={usage.OtherTokens}");
    }
}
