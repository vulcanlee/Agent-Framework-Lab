using AgentFrameworkPersistenceMemory.Agent;
using AgentFrameworkPersistenceMemory.Infrastructure;
using AgentFrameworkPersistenceMemory.Memory;
using AgentFrameworkPersistenceMemory.Recall;
using AgentFrameworkPersistenceMemory.Sessions;
using AgentFrameworkPersistenceMemory.Status;
using AgentFrameworkPersistenceMemory.Workflow;

namespace AgentFrameworkPersistenceMemory.Tests;

public class PmWorkflowTests
{
    [Fact]
    public async Task DiscussAsync_UsesActiveSourceAndWorkItemContext()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        var store = new PersistentMemoryStore(tempPath);
        var sessionManager = new SessionManager();
        var source = new PersistentMemoryRecord(
            "source-001",
            "會員管理需求",
            "整理會員後台的角色與權限需求",
            "會員管理後台需要角色權限管理與審計紀錄查詢。",
            ["會員", "角色權限", "審計紀錄"],
            [],
            [],
            [],
            [
                new WorkItemRecord("W1", "會員角色權限", "建立角色權限設定", "建立角色權限設定", [], [], ["可設定角色"], "Frontend", "建立角色權限設定", "draft"),
                new WorkItemRecord("W2", "審計紀錄查詢", "建立審計紀錄頁面", "建立審計紀錄頁面", [], [], ["可依時間查詢"], "Backend", "建立審計紀錄頁面", "draft")
            ],
            null,
            DateTimeOffset.UtcNow);
        await store.SaveAsync([source], CancellationToken.None);

        sessionManager.SetActiveSource("source-001");
        sessionManager.SetActiveWorkItem("W1");

        var agent = new FakePmAgentService();
        var workflow = new PmWorkflow(
            store,
            sessionManager,
            new MemoryRecallService(new PassThroughEvaluator()),
            agent,
            new AgentStatusReporter(new StringWriter()),
            EngineerRoster.Default);

        var reply = await workflow.DiscussAsync("W1 的驗收條件還缺什麼？", CancellationToken.None);

        Assert.Contains("建議下一步", reply);
        Assert.Equal("source-001", agent.LastDiscussionRequest?.ActiveSource?.Id);
        Assert.Equal("W1", agent.LastDiscussionRequest?.ActiveWorkItem?.Id);
    }

    [Fact]
    public async Task DiscussAsync_DoesNotPersistChanges()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        var store = new PersistentMemoryStore(tempPath);
        var source = new PersistentMemoryRecord(
            "source-001",
            "會員管理需求",
            "整理會員後台的角色與權限需求",
            "會員管理後台需要角色權限管理與審計紀錄查詢。",
            ["會員", "角色權限", "審計紀錄"],
            [],
            [],
            [],
            [
                new WorkItemRecord("W1", "會員角色權限", "建立角色權限設定", "建立角色權限設定", [], [], ["可設定角色"], "Frontend", "建立角色權限設定", "draft")
            ],
            null,
            DateTimeOffset.UtcNow);
        await store.SaveAsync([source], CancellationToken.None);

        var workflow = new PmWorkflow(
            store,
            new SessionManager(),
            new MemoryRecallService(new PassThroughEvaluator()),
            new FakePmAgentService(),
            new AgentStatusReporter(new StringWriter()),
            EngineerRoster.Default);

        _ = await workflow.DiscussAsync("這個需求還有哪些風險？", CancellationToken.None);
        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Equal("建立角色權限設定", loaded[0].WorkItems[0].CurrentDescription);
        Assert.Null(loaded[0].FinalizedOutput);
    }

    [Fact]
    public async Task IngestAndFinalizeFlow_PersistsUpdatedWorkItemsAndFormalOutput()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        var savePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.md");
        var store = new PersistentMemoryStore(tempPath);
        var sessionManager = new SessionManager();
        using var writer = new StringWriter();
        var reporter = new AgentStatusReporter(writer);
        var workflow = new PmWorkflow(
            store,
            sessionManager,
            new MemoryRecallService(new PassThroughEvaluator()),
            new FakePmAgentService(),
            reporter,
            EngineerRoster.Default);

        var ingested = await workflow.IngestSourceAsync("會員管理後台需要角色權限管理，並提供審計紀錄查詢。", CancellationToken.None);
        var revised = await workflow.ReviseWorkItemAsync(ingested.Id, "W1", "加入 email 通知設定與權限異動通知", CancellationToken.None);
        var finalOutput = await workflow.FinalizeSourceAsync(ingested.Id, savePath, CancellationToken.None);
        var persisted = await store.LoadAsync(CancellationToken.None);

        Assert.Equal("source-001", ingested.Id);
        Assert.Equal("W1", revised.Id);
        Assert.Contains("email 通知", revised.CurrentDescription);
        Assert.Contains("# 正式工作需求清單", finalOutput);
        Assert.True(File.Exists(savePath));
        Assert.Equal(ingested.Id, sessionManager.Current.ActiveSourceId);
        Assert.Contains("正在彙整正式工作需求清單", writer.ToString());
        Assert.Equal("finalized", persisted[0].WorkItems[0].Status);
        Assert.NotNull(persisted[0].FinalizedOutput);
    }

    private sealed class PassThroughEvaluator : IMemoryRelevanceEvaluator
    {
        public Task<IReadOnlyList<string>> SelectRelevantMemoryIdsAsync(string issueText, IReadOnlyList<PersistentMemoryRecord> candidates, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class FakePmAgentService : IPmAgentService
    {
        public DiscussionRequest? LastDiscussionRequest { get; private set; }

        public Task<IngestSourceResult> IngestSourceAsync(IngestSourceRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new IngestSourceResult(
                "會員管理需求",
                "針對會員管理後台整理角色權限與審計紀錄需求。",
                ["會員管理後台", "角色權限", "審計紀錄"],
                ["需要先確認權限矩陣。"],
                [
                    new WorkTask("建立角色權限 API", "提供角色權限設定與查詢 API", "Backend"),
                    new WorkTask("建立審計紀錄查詢", "建立審計紀錄查詢與篩選 API", "Backend")
                ],
                [
                    new EngineerAssignment("Backend", "建立角色權限 API"),
                    new EngineerAssignment("Backend", "建立審計紀錄查詢")
                ],
                [
                    new WorkItemDraft("會員角色權限", "建立角色權限設定與查詢流程", ["可設定角色", "可設定權限"], "Frontend"),
                    new WorkItemDraft("審計紀錄查詢", "建立審計紀錄查詢與篩選", ["可依時間查詢", "可依操作人篩選"], "Backend")
                ]));
        }

        public Task<DiscussionResult> DiscussAsync(DiscussionRequest request, CancellationToken cancellationToken)
        {
            LastDiscussionRequest = request;
            return Task.FromResult(new DiscussionResult(
                "W1 目前缺少更明確的權限矩陣與驗收條件。",
                ["W1"],
                ["先補齊角色矩陣", "再確認是否需要審批流程"]));
        }

        public Task<WorkItemRevisionResult> ReviseWorkItemAsync(WorkItemRevisionRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new WorkItemRevisionResult(
                "建立角色權限設定與查詢流程，並加入 email 通知設定與權限異動通知",
                ["可設定角色", "可設定權限", "權限異動時發送 email 通知"],
                ["加入通知需求"],
                "建立角色權限設定與查詢流程，並加入 email 通知設定與權限異動通知",
                "Frontend",
                "in_review"));
        }

        public Task<FinalizeSourceResult> FinalizeSourceAsync(FinalizeSourceRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new FinalizeSourceResult(
                """
                # 正式工作需求清單
                Source: source-001

                1. 會員角色權限
                - 最終說明：建立角色權限設定與查詢流程，並加入 email 通知設定與權限異動通知
                - 驗收重點：可設定角色、可設定權限、權限異動時發送 email 通知
                - 指派建議：Frontend
                """));
        }
    }
}
