using AgentFrameworkPersistenceMemory.Memory;

namespace AgentFrameworkPersistenceMemory.Recall;

public interface IMemoryRelevanceEvaluator
{
    Task<IReadOnlyList<string>> SelectRelevantMemoryIdsAsync(
        string issueText,
        IReadOnlyList<PersistentMemoryRecord> candidates,
        CancellationToken cancellationToken);
}
