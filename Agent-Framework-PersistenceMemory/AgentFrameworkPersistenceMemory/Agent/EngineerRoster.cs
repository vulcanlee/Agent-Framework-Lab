namespace AgentFrameworkPersistenceMemory.Agent;

public sealed record EngineerProfile(string Name, string Specialty, string Description);

public sealed class EngineerRoster(IReadOnlyList<EngineerProfile> engineers)
{
    public static EngineerRoster Default { get; } = new([
        new EngineerProfile("Backend", "API 與資料流程", "負責 API、商業邏輯、資料模型與儲存流程。"),
        new EngineerProfile("Frontend", "介面與互動流程", "負責管理介面、表單操作與使用體驗。"),
        new EngineerProfile("QA", "測試與驗收", "負責測試情境、驗收條件與品質風險確認。")
    ]);

    public IReadOnlyList<EngineerProfile> Engineers { get; } = engineers;
}
