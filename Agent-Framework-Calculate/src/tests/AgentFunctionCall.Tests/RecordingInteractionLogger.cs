using AgentFunctionCall.Services;

namespace AgentFunctionCall.Tests;

internal sealed class RecordingInteractionLogger : IInteractionLogger
{
    public List<(string Title, string Content)> Entries { get; } = [];

    public void LogSection(string title, string content)
    {
        Entries.Add((title, content));
    }
}
