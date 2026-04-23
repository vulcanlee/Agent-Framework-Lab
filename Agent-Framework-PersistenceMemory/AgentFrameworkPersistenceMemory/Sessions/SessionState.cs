namespace AgentFrameworkPersistenceMemory.Sessions;

public sealed class SessionState
{
    public string SessionId { get; init; } = Guid.NewGuid().ToString("N");

    public List<string> UserInputs { get; } = [];

    public List<string> AgentNotes { get; } = [];

    public List<string> RecalledMemoryIds { get; } = [];

    public string? ActiveSourceId { get; set; }

    public string? ActiveWorkItemId { get; set; }

    public bool IsIngestMode { get; set; }

    public List<string> IngestBuffer { get; } = [];
}
