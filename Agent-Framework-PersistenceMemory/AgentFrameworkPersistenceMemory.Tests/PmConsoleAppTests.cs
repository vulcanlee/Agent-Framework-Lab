using AgentFrameworkPersistenceMemory.Agent;
using AgentFrameworkPersistenceMemory.Memory;
using AgentFrameworkPersistenceMemory.Sessions;
using AgentFrameworkPersistenceMemory.Workflow;

namespace AgentFrameworkPersistenceMemory.Tests;

public class PmConsoleAppTests
{
    [Fact]
    public async Task HelpCommand_ListsSlashCommandsAndExamples()
    {
        using var output = new StringWriter();
        var app = CreateApp(output);

        await app.HandleInputAsync("/help", CancellationToken.None);

        var text = output.ToString();
        Assert.Contains("/source remove <source-id>", text);
        Assert.Contains("/work add <title>", text);
        Assert.Contains("/ingest <text-file-path>", text);
    }

    [Fact]
    public async Task HelpCommand_WithBomPrefix_IsStillRecognized()
    {
        using var output = new StringWriter();
        var app = CreateApp(output);

        await app.HandleInputAsync("\uFEFF/help", CancellationToken.None);

        Assert.Contains("可用指令", output.ToString());
    }

    [Fact]
    public async Task IngestMode_AppendsLinesUntilEnd()
    {
        using var output = new StringWriter();
        var workflow = new FakeWorkflow();
        var app = new PmConsoleApp(
            new PersistentMemoryStore(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json")),
            new SessionManager(),
            workflow,
            output);

        await app.HandleInputAsync("/ingest", CancellationToken.None);
        await app.HandleInputAsync("會員管理後台需要角色權限管理", CancellationToken.None);
        await app.HandleInputAsync("同時需要審計紀錄查詢", CancellationToken.None);
        await app.HandleInputAsync("/end", CancellationToken.None);

        Assert.Equal("會員管理後台需要角色權限管理\n同時需要審計紀錄查詢", workflow.LastIngestedRawInput.Replace("\r", string.Empty));
        Assert.Contains("已拆解出 2 個工作項目。", output.ToString());
    }

    [Fact]
    public async Task IngestFromFile_ReadsUtf8TextFile()
    {
        using var output = new StringWriter();
        var workflow = new FakeWorkflow();
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(filePath, "會員管理後台需要角色權限管理", new System.Text.UTF8Encoding(false));
        var app = new PmConsoleApp(
            new PersistentMemoryStore(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json")),
            new SessionManager(),
            workflow,
            output);

        await app.HandleInputAsync($"/ingest {filePath}", CancellationToken.None);

        Assert.Equal("會員管理後台需要角色權限管理", workflow.LastIngestedRawInput);
        Assert.Contains("已從檔案建立來源 source-001", output.ToString());
    }

    [Fact]
    public async Task WorkReview_AndUpdate_MissingWorkItem_DoNotCrash()
    {
        using var output = new StringWriter();
        var app = CreateApp(output);

        await app.HandleInputAsync("/work list source-001", CancellationToken.None);
        await app.HandleInputAsync("/work review w5", CancellationToken.None);
        await app.HandleInputAsync("/work update w5 測試修正", CancellationToken.None);
        await app.HandleInputAsync("/work review w1", CancellationToken.None);

        var text = output.ToString();
        Assert.Contains("找不到工作項目 w5。", text);
        Assert.Contains("W1 - 會員角色權限", text);
        Assert.DoesNotContain("啟動失敗", text);
    }

    [Fact]
    public async Task WorkAddAndRemove_UseCurrentSource()
    {
        using var output = new StringWriter();
        var workflow = new FakeWorkflow();
        var app = new PmConsoleApp(
            new PersistentMemoryStore(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json")),
            new SessionManager(),
            workflow,
            output);

        await app.HandleInputAsync("/work list source-001", CancellationToken.None);
        await app.HandleInputAsync("/work add 權限異動通知 --desc 加入 email 與站內通知 --owner Backend --accept 可設定通知內容;可關閉通知", CancellationToken.None);
        await app.HandleInputAsync("/work remove W2", CancellationToken.None);

        var text = output.ToString();
        Assert.Contains("已新增工作項目 W3：權限異動通知", text);
        Assert.Contains("已移除工作項目 W2。", text);
    }

    [Fact]
    public async Task SourceRemove_RemovesRecordWithoutCrashing()
    {
        using var output = new StringWriter();
        var app = CreateApp(output);

        await app.HandleInputAsync("/source remove source-001", CancellationToken.None);
        await app.HandleInputAsync("/source remove source-999", CancellationToken.None);

        var text = output.ToString();
        Assert.Contains("已移除來源 source-001。", text);
        Assert.Contains("找不到來源 source-999。", text);
    }

    private static PmConsoleApp CreateApp(StringWriter output)
        => new(
            new PersistentMemoryStore(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json")),
            new SessionManager(),
            new FakeWorkflow(),
            output);

    private sealed class FakeWorkflow : IPmWorkflow
    {
        public string LastIngestedRawInput { get; private set; } = string.Empty;

        public Task<PersistentMemoryRecord> GetSourceAsync(string sourceId, CancellationToken cancellationToken)
        {
            if (!string.Equals(sourceId, "source-001", StringComparison.OrdinalIgnoreCase))
            {
                throw new UserFacingException($"找不到來源 {sourceId}。");
            }

            return Task.FromResult(new PersistentMemoryRecord(
                sourceId,
                "會員管理需求",
                "整理會員後台的角色與權限需求",
                "raw",
                ["會員", "角色權限"],
                [],
                [],
                [],
                [
                    new WorkItemRecord("W1", "會員角色權限", "建立角色權限設定", "建立角色權限設定", [], [], ["可設定角色", "可設定權限"], "Frontend", "建立角色權限設定", "in_review"),
                    new WorkItemRecord("W2", "審計紀錄查詢", "建立審計紀錄頁面", "建立審計紀錄頁面", [], [], ["可依時間查詢"], "Backend", "建立審計紀錄頁面", "draft")
                ],
                null,
                DateTimeOffset.UtcNow));
        }

        public Task<PersistentMemoryRecord> IngestSourceAsync(string rawInput, CancellationToken cancellationToken)
        {
            LastIngestedRawInput = rawInput;
            return Task.FromResult(new PersistentMemoryRecord(
                "source-001",
                "會員管理需求",
                "整理會員後台的角色與權限需求",
                rawInput,
                ["會員", "角色權限"],
                [],
                [],
                [],
                [
                    new WorkItemRecord("W1", "會員角色權限", "建立角色權限設定", "建立角色權限設定", [], [], ["可設定角色"], "Frontend", "建立角色權限設定", "draft"),
                    new WorkItemRecord("W2", "審計紀錄查詢", "建立審計紀錄頁面", "建立審計紀錄頁面", [], [], ["可依時間查詢"], "Backend", "建立審計紀錄頁面", "draft")
                ],
                null,
                DateTimeOffset.UtcNow));
        }

        public Task<WorkItemRecord> ReviseWorkItemAsync(string sourceId, string workItemId, string feedback, CancellationToken cancellationToken)
        {
            if (!string.Equals(workItemId, "W1", StringComparison.OrdinalIgnoreCase))
            {
                throw new UserFacingException($"找不到工作項目 {workItemId}。");
            }

            return Task.FromResult(new WorkItemRecord("W1", "會員角色權限", "建立角色權限設定", feedback, ["已檢討"], ["加入使用者建議"], ["可設定角色"], "Frontend", feedback, "in_review"));
        }

        public Task<WorkItemRecord> AddWorkItemAsync(string sourceId, ManualWorkItemDraft draft, CancellationToken cancellationToken)
            => Task.FromResult(new WorkItemRecord("W3", draft.Title, draft.Description, draft.Description, [], [], draft.AcceptanceCriteria, draft.SuggestedEngineer, draft.Description, "draft"));

        public Task RemoveWorkItemAsync(string sourceId, string workItemId, CancellationToken cancellationToken)
        {
            if (!string.Equals(workItemId, "W2", StringComparison.OrdinalIgnoreCase))
            {
                throw new UserFacingException($"找不到工作項目 {workItemId}。");
            }

            return Task.CompletedTask;
        }

        public Task RemoveSourceAsync(string sourceId, CancellationToken cancellationToken)
        {
            if (!string.Equals(sourceId, "source-001", StringComparison.OrdinalIgnoreCase))
            {
                throw new UserFacingException($"找不到來源 {sourceId}。");
            }

            return Task.CompletedTask;
        }

        public Task<string> FinalizeSourceAsync(string sourceId, string? savePath, CancellationToken cancellationToken)
            => Task.FromResult("# 正式工作需求清單");

        public Task<IReadOnlyList<PersistentMemoryRecord>> LoadMemoryAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<PersistentMemoryRecord>>([]);
    }
}
