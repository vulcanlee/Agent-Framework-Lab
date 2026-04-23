using AgentFrameworkPersistenceMemory.Memory;
using AgentFrameworkPersistenceMemory.Recall;

namespace AgentFrameworkPersistenceMemory.Tests;

public class MemoryRecallServiceTests
{
    [Fact]
    public async Task RecallRelevantMemoriesAsync_FiltersCandidatesBeforeCallingEvaluator()
    {
        var evaluator = new FakeMemoryRelevanceEvaluator("source-002");
        var service = new MemoryRecallService(evaluator);
        var memories = new[]
        {
            new PersistentMemoryRecord(
                "source-001",
                "會員註冊需求",
                "整理會員註冊與 email 驗證",
                "會議逐字稿 A",
                ["會員", "註冊"],
                [],
                [],
                [],
                [],
                null,
                DateTimeOffset.UtcNow),
            new PersistentMemoryRecord(
                "source-002",
                "會員權限需求",
                "整理角色與權限控制",
                "會議逐字稿 B",
                ["會員", "權限"],
                [],
                [],
                [],
                [],
                null,
                DateTimeOffset.UtcNow)
        };

        var recalled = await service.RecallRelevantMemoriesAsync("請延伸之前討論過的角色與權限控制需求", memories, CancellationToken.None);

        Assert.Single(recalled);
        Assert.Equal("source-002", recalled[0].Id);
        Assert.Equal(["source-002"], evaluator.CandidateIds);
    }

    private sealed class FakeMemoryRelevanceEvaluator(params string[] approvedIds) : IMemoryRelevanceEvaluator
    {
        private readonly HashSet<string> _approvedIds = [.. approvedIds];

        public List<string> CandidateIds { get; } = [];

        public Task<IReadOnlyList<string>> SelectRelevantMemoryIdsAsync(string issueText, IReadOnlyList<PersistentMemoryRecord> candidates, CancellationToken cancellationToken)
        {
            CandidateIds.Clear();
            CandidateIds.AddRange(candidates.Select(candidate => candidate.Id));
            IReadOnlyList<string> result = candidates.Where(candidate => _approvedIds.Contains(candidate.Id)).Select(candidate => candidate.Id).ToList();
            return Task.FromResult(result);
        }
    }
}
