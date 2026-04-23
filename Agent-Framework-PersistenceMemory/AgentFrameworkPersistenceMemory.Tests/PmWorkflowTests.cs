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

    [Fact]
    public async Task AddAndRemoveWorkItem_UpdatesPersistentMemory()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        var store = new PersistentMemoryStore(tempPath);
        var workflow = new PmWorkflow(
            store,
            new SessionManager(),
            new MemoryRecallService(new PassThroughEvaluator()),
            new FakePmAgentService(),
            new AgentStatusReporter(new StringWriter()),
            EngineerRoster.Default);

        var ingested = await workflow.IngestSourceAsync("會員後台需求", CancellationToken.None);
        var added = await workflow.AddWorkItemAsync(
            ingested.Id,
            new ManualWorkItemDraft("權限異動通知", "加入 email 與站內通知", "Backend", ["可設定通知內容"]),
            CancellationToken.None);
        await workflow.RemoveWorkItemAsync(ingested.Id, "W1", CancellationToken.None);
        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Equal("W3", added.Id);
        Assert.DoesNotContain(loaded[0].WorkItems, item => item.Id == "W1");
        Assert.Contains(loaded[0].WorkItems, item => item.Id == "W3");
    }

    [Fact]
    public async Task RemoveSourceAsync_DeletesWholeRecord()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        var store = new PersistentMemoryStore(tempPath);
        var sessionManager = new SessionManager();
        var workflow = new PmWorkflow(
            store,
            sessionManager,
            new MemoryRecallService(new PassThroughEvaluator()),
            new FakePmAgentService(),
            new AgentStatusReporter(new StringWriter()),
            EngineerRoster.Default);

        var ingested = await workflow.IngestSourceAsync("會員後台需求", CancellationToken.None);
        await workflow.RemoveSourceAsync(ingested.Id, CancellationToken.None);
        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Empty(loaded);
        Assert.Null(sessionManager.Current.ActiveSourceId);
        Assert.Null(sessionManager.Current.ActiveWorkItemId);
    }

    [Fact]
    public async Task IngestSourceAsync_UsesFallbackWhenModelOutputIsOffTopic()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        var store = new PersistentMemoryStore(tempPath);
        var workflow = new PmWorkflow(
            store,
            new SessionManager(),
            new MemoryRecallService(new PassThroughEvaluator()),
            new AgentFrameworkPmAgentService(
                new FakeGitHubModelsClient("""
                {
                  "title": "Email 驗證流程",
                  "summary": "優化註冊與 email 驗證流程。",
                  "keywords": ["email", "註冊"],
                  "decisions": [],
                  "tasks": [{"title":"Email API","description":"建立 email 驗證 API","suggestedEngineer":"Backend"}],
                  "assignments": [{"engineer":"Backend","taskTitle":"Email API"}],
                  "workItems": [{"title":"Email 驗證","description":"建立 email 驗證流程","acceptanceCriteria":["可發送驗證信"],"suggestedEngineer":"Backend"}]
                }
                """),
                new AgentStatusReporter(new StringWriter())),
            new AgentStatusReporter(new StringWriter()),
            EngineerRoster.Default);

        var ingested = await workflow.IngestSourceAsync("會員管理後台需要角色權限管理與審計紀錄查詢。", CancellationToken.None);

        Assert.Contains("需要重新確認的需求", ingested.Title);
        Assert.Contains("會員管理後台", ingested.Summary);
        Assert.Single(ingested.WorkItems);
    }

    private sealed class PassThroughEvaluator : IMemoryRelevanceEvaluator
    {
        public Task<IReadOnlyList<string>> SelectRelevantMemoryIdsAsync(string issueText, IReadOnlyList<PersistentMemoryRecord> candidates, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class FakePmAgentService : IPmAgentService
    {
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

    private sealed class FakeGitHubModelsClient(string json) : IGitHubModelsClient
    {
        public Task<ModelCompletionResult> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
            => Task.FromResult(new ModelCompletionResult(json, new ModelUsage(10, 20, 0)));
    }
}
