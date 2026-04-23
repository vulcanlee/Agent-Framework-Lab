using AgentFrameworkPersistenceMemory.Memory;

namespace AgentFrameworkPersistenceMemory.Workflow;

public interface IPmWorkflow
{
    Task<PersistentMemoryRecord> IngestSourceAsync(string rawInput, CancellationToken cancellationToken);

    Task<WorkItemRecord> ReviseWorkItemAsync(string sourceId, string workItemId, string feedback, CancellationToken cancellationToken);

    Task<string> FinalizeSourceAsync(string sourceId, string? savePath, CancellationToken cancellationToken);

    Task<PersistentMemoryRecord> GetSourceAsync(string sourceId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PersistentMemoryRecord>> LoadMemoryAsync(CancellationToken cancellationToken);
}
