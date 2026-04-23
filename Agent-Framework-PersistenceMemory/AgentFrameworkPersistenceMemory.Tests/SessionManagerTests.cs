using AgentFrameworkPersistenceMemory.Sessions;

namespace AgentFrameworkPersistenceMemory.Tests;

public class SessionManagerTests
{
    [Fact]
    public void NewSession_ClearsContextAndEditingState()
    {
        var manager = new SessionManager();

        manager.AddUserInput("請分析會員管理後台需求");
        manager.AddAgentNote("已建立來源 source-001");
        manager.SetActiveSource("source-001");
        manager.SetActiveWorkItem("W1");
        manager.StartIngestMode();
        manager.AppendIngestLine("會員管理後台需要角色權限管理");

        manager.NewSession();

        Assert.Empty(manager.Current.UserInputs);
        Assert.Empty(manager.Current.AgentNotes);
        Assert.Empty(manager.Current.RecalledMemoryIds);
        Assert.Null(manager.Current.ActiveSourceId);
        Assert.Null(manager.Current.ActiveWorkItemId);
        Assert.False(manager.Current.IsIngestMode);
        Assert.Empty(manager.Current.IngestBuffer);
        Assert.Equal("目前 session 還沒有任何內容。", manager.GetSummary());
    }
}
