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
                "會員驗證需求",
                "處理會員登入與 email 驗證。",
                "原始需求 A",
                ["會員", "驗證"],
                [],
                [],
                [],
                [],
                null,
                DateTimeOffset.UtcNow),
            new PersistentMemoryRecord(
                "source-002",
                "會員權限需求",
                "整理角色權限與後台管理需求。",
                "原始需求 B",
                ["會員", "權限"],
                [],
                [],
                [],
                [],
                null,
                DateTimeOffset.UtcNow)
        };

        var recalled = await service.RecallRelevantMemoriesAsync("想討論會員後台的權限設定", memories, CancellationToken.None);

        Assert.Single(recalled);
        Assert.Equal("source-002", recalled[0].Id);
        Assert.Contains("source-002", evaluator.CandidateIds);
        Assert.Equal(2, evaluator.CandidateIds.Count);
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
