using AgentFrameworkPersistenceMemory.Memory;

namespace AgentFrameworkPersistenceMemory.Agent;

public sealed record WorkItemDraft(
    string Title,
    string Description,
    IReadOnlyList<string> AcceptanceCriteria,
    string SuggestedEngineer);

public sealed record IngestSourceRequest(
    string RawInput,
    string SessionSummary,
    IReadOnlyList<PersistentMemoryRecord> RecalledMemories,
    EngineerRoster EngineerRoster);

public sealed record IngestSourceResult(
    string Title,
    string Summary,
    IReadOnlyList<string> Keywords,
    IReadOnlyList<string> Decisions,
    IReadOnlyList<WorkTask> Tasks,
    IReadOnlyList<EngineerAssignment> Assignments,
    IReadOnlyList<WorkItemDraft> WorkItems);

public sealed record WorkItemRevisionRequest(
    PersistentMemoryRecord Source,
    WorkItemRecord WorkItem,
    string Feedback,
    string SessionSummary);

public sealed record WorkItemRevisionResult(
    string UpdatedDescription,
    IReadOnlyList<string> AcceptanceCriteria,
    IReadOnlyList<string> DiscussionNotes,
    string FinalizedDescription,
    string SuggestedEngineer,
    string Status);

public sealed record FinalizeSourceRequest(
    PersistentMemoryRecord Source,
    string SessionSummary);

public sealed record FinalizeSourceResult(string FormalizedOutput);

public sealed record DiscussionRequest(
    string UserMessage,
    string SessionSummary,
    PersistentMemoryRecord? ActiveSource,
    WorkItemRecord? ActiveWorkItem,
    IReadOnlyList<PersistentMemoryRecord> RecalledMemories);

public sealed record DiscussionResult(
    string Reply,
    IReadOnlyList<string> ReferencedWorkItemIds,
    IReadOnlyList<string> SuggestedNextActions);

public interface IPmAgentService
{
    Task<IngestSourceResult> IngestSourceAsync(IngestSourceRequest request, CancellationToken cancellationToken);

    Task<DiscussionResult> DiscussAsync(DiscussionRequest request, CancellationToken cancellationToken);

    Task<WorkItemRevisionResult> ReviseWorkItemAsync(WorkItemRevisionRequest request, CancellationToken cancellationToken);

    Task<FinalizeSourceResult> FinalizeSourceAsync(FinalizeSourceRequest request, CancellationToken cancellationToken);
}
