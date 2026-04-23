using System.Text;
using AgentFrameworkPersistenceMemory.Memory;

namespace AgentFrameworkPersistenceMemory.Tests;

public class PersistentMemoryStoreTests
{
    [Fact]
    public async Task SaveAsync_PersistsUtf8ChineseAndWorkItems()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        var store = new PersistentMemoryStore(tempPath);
        var record = new PersistentMemoryRecord(
            "source-001",
            "會員管理需求",
            "整理會員管理後台的角色權限與審計紀錄需求。",
            "會員管理後台需要角色權限管理與審計紀錄查詢。",
            ["會員", "角色權限"],
            ["先確認權限矩陣。"],
            [new WorkTask("建立角色權限 API", "提供角色權限設定與查詢 API", "Backend")],
            [new EngineerAssignment("Backend", "建立角色權限 API")],
            [
                new WorkItemRecord(
                    "W1",
                    "會員角色權限",
                    "建立會員角色權限設定",
                    "建立會員角色權限設定，並加入 email 通知",
                    ["加入通知需求"],
                    ["加入 email 驗證與通知機制"],
                    ["可設定角色", "可設定權限"],
                    "Frontend",
                    "建立會員角色權限設定，並加入 email 通知",
                    "in_review")
            ],
            "# 正式工作需求清單",
            DateTimeOffset.UtcNow);

        await store.SaveAsync([record], CancellationToken.None);

        var raw = await File.ReadAllTextAsync(tempPath, Encoding.UTF8);
        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Contains("會員管理需求", raw);
        Assert.Single(loaded);
        Assert.Single(loaded[0].WorkItems);
        Assert.Equal("建立會員角色權限設定，並加入 email 通知", loaded[0].WorkItems[0].CurrentDescription);
    }
}
