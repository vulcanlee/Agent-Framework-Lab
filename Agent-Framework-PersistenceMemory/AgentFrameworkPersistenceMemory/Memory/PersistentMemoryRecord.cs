namespace AgentFrameworkPersistenceMemory.Memory;

public sealed record PersistentMemoryRecord(
    string Id,
    string Title,
    string Summary,
    string RawInput,
    IReadOnlyList<string> Keywords,
    IReadOnlyList<string> Decisions,
    IReadOnlyList<WorkTask> Tasks,
    IReadOnlyList<EngineerAssignment> Assignments,
    IReadOnlyList<WorkItemRecord> WorkItems,
    string? FinalizedOutput,
    DateTimeOffset UpdatedAt);

public sealed record WorkTask(string Title, string Description, string SuggestedEngineer);

public sealed record EngineerAssignment(string Engineer, string TaskTitle);

public sealed record WorkItemRecord(
    string Id,
    string Title,
    string OriginalDescription,
    string CurrentDescription,
    IReadOnlyList<string> DiscussionNotes,
    IReadOnlyList<string> RevisionSuggestions,
    IReadOnlyList<string> AcceptanceCriteria,
    string SuggestedEngineer,
    string FinalizedDescription,
    string Status);

public sealed record ManualWorkItemDraft(
    string Title,
    string Description,
    string SuggestedEngineer,
    IReadOnlyList<string> AcceptanceCriteria);
