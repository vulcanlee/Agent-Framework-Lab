using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgentFrameworkPersistenceMemory.Infrastructure;
using AgentFrameworkPersistenceMemory.Memory;
using Microsoft.Agents.AI;

namespace AgentFrameworkPersistenceMemory.Agent;

public sealed class AgentFrameworkPmAgentService : IPmAgentService
{
    private readonly IGitHubModelsClient _client;
    private readonly PmAgentFrameworkAgent _agent;
    private AgentSession? _session;

    public AgentFrameworkPmAgentService(IGitHubModelsClient client)
    {
        _client = client;
        _agent = new PmAgentFrameworkAgent("PM Agent", CompletePromptAsync);
    }

    public void ResetSession()
    {
        _session = null;
    }

    public async Task<IngestSourceResult> IngestSourceAsync(IngestSourceRequest request, CancellationToken cancellationToken)
    {
        var sourceKeywords = ExtractSourceKeywords(request.RawInput);
        var prompt = $@"
你是 PM Agent，負責把原始需求整理成可追蹤的工作項目。
你只能根據使用者提供的原始材料、回填記憶與工程師名單回答，不可改寫成其他主題，不可自行發明未出現的新產品方向。
請先辨識原始材料中的核心名詞、功能模組、角色與限制；如果資訊不足，可以保留待確認問題，但不能把需求轉成其他題目。
請只輸出 JSON，格式如下：
{{
  ""title"": ""string"",
  ""summary"": ""string"",
  ""keywords"": [""string""],
  ""decisions"": [""string""],
  ""tasks"": [{{""title"":""string"",""description"":""string"",""suggestedEngineer"":""string""}}],
  ""assignments"": [{{""engineer"":""string"",""taskTitle"":""string""}}],
  ""workItems"": [{{""title"":""string"",""description"":""string"",""acceptanceCriteria"":[""string""],""suggestedEngineer"":""string""}}]
}}

必須遵守：
1. 任何輸出都必須與原始材料中的主題一致。
2. 若無法確認，請在 summary 說明「需要重新確認」，不要改寫成其他主題。
3. workItems 的標題與描述必須直接對應原始材料中的需求。
4. 盡量保留這些關鍵詞：{string.Join("、", sourceKeywords)}

目前 session 摘要：
{request.SessionSummary}

回填的相關永久記憶：
{FormatRecalledMemories(request.RecalledMemories)}

工程師名單：
{FormatEngineers(request.EngineerRoster)}

原始需求：
{request.RawInput}";

        var parsed = await RunJsonPromptAsync<IngestSourceResponse>(prompt, cancellationToken);
        var result = new IngestSourceResult(
            parsed.Title ?? "需要重新確認的需求",
            parsed.Summary ?? string.Empty,
            parsed.Keywords ?? [],
            parsed.Decisions ?? [],
            parsed.Tasks?.Select(task => new WorkTask(task.Title ?? string.Empty, task.Description ?? string.Empty, task.SuggestedEngineer ?? "Backend")).ToList() ?? [],
            parsed.Assignments?.Select(item => new EngineerAssignment(item.Engineer ?? "Backend", item.TaskTitle ?? string.Empty)).ToList() ?? [],
            parsed.WorkItems?.Select(item => new WorkItemDraft(item.Title ?? string.Empty, item.Description ?? string.Empty, item.AcceptanceCriteria ?? [], item.SuggestedEngineer ?? "Backend")).ToList() ?? []);

        return IsFaithfulToSource(sourceKeywords, BuildIngestValidationText(result))
            ? result
            : BuildFallbackIngestResult(request.RawInput, sourceKeywords);
    }

    public async Task<WorkItemRevisionResult> ReviseWorkItemAsync(WorkItemRevisionRequest request, CancellationToken cancellationToken)
    {
        var sourceKeywords = ExtractSourceKeywords($"{request.Source.RawInput}{Environment.NewLine}{request.WorkItem.Title}{Environment.NewLine}{request.WorkItem.OriginalDescription}");
        var prompt = $@"
你是 PM Agent，正在修正單一 work item。
你只能根據 source 原文、目前 work item 內容與使用者這次的修正建議回答，不可把主題改成其他需求。
請只輸出 JSON，格式如下：
{{
  ""updatedDescription"": ""string"",
  ""acceptanceCriteria"": [""string""],
  ""discussionNotes"": [""string""],
  ""finalizedDescription"": ""string"",
  ""suggestedEngineer"": ""string"",
  ""status"": ""draft|in_review|finalized""
}}

必須遵守：
1. 更新後的內容必須維持在原始需求主題內。
2. 不得新增原文從未提到的產品方向。
3. 若使用者建議不足以修改，就保留原主題並寫成更明確的描述。
4. 盡量保留這些關鍵詞：{string.Join("、", sourceKeywords)}

來源摘要：
{request.Source.Summary}

來源原文：
{request.Source.RawInput}

工作項目：{request.WorkItem.Title}
原始描述：{request.WorkItem.OriginalDescription}
目前版本：{request.WorkItem.CurrentDescription}
目前驗收條件：{string.Join("、", request.WorkItem.AcceptanceCriteria)}
Session 摘要：{request.SessionSummary}

使用者修正建議：
{request.Feedback}";

        var parsed = await RunJsonPromptAsync<WorkItemRevisionResponse>(prompt, cancellationToken);
        var result = new WorkItemRevisionResult(
            parsed.UpdatedDescription ?? request.WorkItem.CurrentDescription,
            parsed.AcceptanceCriteria ?? request.WorkItem.AcceptanceCriteria,
            parsed.DiscussionNotes ?? [],
            parsed.FinalizedDescription ?? parsed.UpdatedDescription ?? request.WorkItem.FinalizedDescription,
            parsed.SuggestedEngineer ?? request.WorkItem.SuggestedEngineer,
            parsed.Status ?? "in_review");

        return IsFaithfulToSource(sourceKeywords, $"{result.UpdatedDescription}\n{result.FinalizedDescription}\n{string.Join('\n', result.AcceptanceCriteria)}")
            ? result
            : new WorkItemRevisionResult(
                request.WorkItem.CurrentDescription,
                request.WorkItem.AcceptanceCriteria,
                ["模型輸出偏離原始需求，已保留原內容並請重新確認。"],
                request.WorkItem.FinalizedDescription,
                request.WorkItem.SuggestedEngineer,
                request.WorkItem.Status);
    }

    public async Task<FinalizeSourceResult> FinalizeSourceAsync(FinalizeSourceRequest request, CancellationToken cancellationToken)
    {
        var sourceKeywords = ExtractSourceKeywords($"{request.Source.RawInput}{Environment.NewLine}{request.Source.Title}{Environment.NewLine}{request.Source.Summary}");
        var prompt = $@"
你是 PM Agent，正在把一個 source 的 work items 輸出成正式工作需求清單。
你只能整合 source 原文與目前保存的 work items，不可新增未出現在原文或討論紀錄中的新主題。
請只輸出 JSON，格式如下：
{{
  ""formalizedOutput"": ""markdown string""
}}

必須遵守：
1. 正式清單必須忠於原始需求。
2. 若模型無法確定細節，可保留待確認事項，但不可改題。
3. 盡量保留這些關鍵詞：{string.Join("、", sourceKeywords)}

來源標題：
{request.Source.Title}
來源摘要：{request.Source.Summary}
來源原文：
{request.Source.RawInput}

目前 work items：
{FormatWorkItems(request.Source.WorkItems)}
Session 摘要：{request.SessionSummary}";

        var parsed = await RunJsonPromptAsync<FinalizeSourceResponse>(prompt, cancellationToken);
        var output = parsed.FormalizedOutput ?? BuildFallbackFormalizedOutput(request.Source);
        return new FinalizeSourceResult(
            IsFaithfulToSource(sourceKeywords, output)
                ? output
                : BuildFallbackFormalizedOutput(request.Source));
    }

    private async Task<T> RunJsonPromptAsync<T>(string prompt, CancellationToken cancellationToken) where T : class
    {
        _session ??= await _agent.CreateSessionAsync(cancellationToken);
        var content = await _agent.RunAsync(prompt, _session, cancellationToken: cancellationToken);
        var parsed = JsonSerializer.Deserialize<T>(content.Text, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return parsed ?? throw new InvalidOperationException("PM Agent 回傳的 JSON 無法解析。");
    }

    private Task<string> CompletePromptAsync(string prompt, CancellationToken cancellationToken)
    {
        const string systemPrompt = """
你是負責產品需求分析的 PM Agent。
你的首要原則是忠於使用者提供的原始需求與既有討論紀錄。
不能把需求改寫成其他主題，不能捏造不存在的功能，也不能輸出 JSON 以外的內容。
如果資訊不足，請明確指出需要重新確認，而不是自行補成別的題目。
""";
        return _client.CompleteAsync(systemPrompt, prompt, cancellationToken);
    }

    private static string FormatRecalledMemories(IReadOnlyList<PersistentMemoryRecord> memories)
        => memories.Count == 0
            ? "無"
            : string.Join(Environment.NewLine, memories.Select(memory => $"- {memory.Id} | {memory.Title} | {memory.Summary}"));

    private static string FormatEngineers(EngineerRoster roster)
        => string.Join(Environment.NewLine, roster.Engineers.Select(engineer => $"- {engineer.Name}: {engineer.Specialty} / {engineer.Description}"));

    private static string FormatWorkItems(IReadOnlyList<WorkItemRecord> items)
        => string.Join(Environment.NewLine, items.Select(item =>
            $"- {item.Id} {item.Title} | 目前版本：{item.CurrentDescription} | 驗收條件：{string.Join("、", item.AcceptanceCriteria)} | 建議指派：{item.SuggestedEngineer}"));

    private static string BuildFallbackFormalizedOutput(PersistentMemoryRecord source)
    {
        var lines = new List<string>
        {
            "# 正式工作需求清單",
            $"Source: {source.Id}",
            $"標題：{source.Title}",
            $"摘要：{source.Summary}",
            string.Empty
        };

        for (var index = 0; index < source.WorkItems.Count; index++)
        {
            var item = source.WorkItems[index];
            lines.Add($"{index + 1}. {item.Title}");
            lines.Add($"- 最終說明：{item.FinalizedDescription}");
            lines.Add($"- 驗收重點：{string.Join("、", item.AcceptanceCriteria)}");
            lines.Add($"- 指派建議：{item.SuggestedEngineer}");
            lines.Add(string.Empty);
        }

        return string.Join(Environment.NewLine, lines).TrimEnd();
    }

    private static IReadOnlyList<string> ExtractSourceKeywords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return Regex.Matches(text, @"[\p{IsCJKUnifiedIdeographs}A-Za-z0-9]{2,}")
            .Select(match => match.Value.Trim())
            .Where(value => value.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(value => value.Length)
            .Take(8)
            .ToList();
    }

    private static bool IsFaithfulToSource(IReadOnlyList<string> sourceKeywords, string candidateText)
    {
        if (sourceKeywords.Count == 0)
        {
            return true;
        }

        var normalizedCandidate = candidateText.Normalize(NormalizationForm.FormC);
        var matches = sourceKeywords.Count(keyword =>
            normalizedCandidate.Contains(keyword, StringComparison.OrdinalIgnoreCase));

        return matches >= Math.Min(2, sourceKeywords.Count);
    }

    private static string BuildIngestValidationText(IngestSourceResult result)
    {
        var workItemText = string.Join(
            Environment.NewLine,
            result.WorkItems.Select(item => $"{item.Title} {item.Description} {string.Join(' ', item.AcceptanceCriteria)}"));

        return $"{result.Title}\n{result.Summary}\n{workItemText}";
    }

    private static IngestSourceResult BuildFallbackIngestResult(string rawInput, IReadOnlyList<string> sourceKeywords)
    {
        var topic = sourceKeywords.Count == 0 ? "原始需求" : string.Join("、", sourceKeywords.Take(3));
        var summary = $"模型輸出與原始需求關鍵詞不一致，請重新確認以下主題：{topic}。目前先保留為待確認需求，避免誤拆成其他題目。";
        return new IngestSourceResult(
            $"需要重新確認的需求：{topic}",
            summary,
            sourceKeywords,
            ["先確認需求範圍與驗收條件，避免偏離原始材料。"],
            [new WorkTask("確認需求範圍", $"針對 {topic} 重新整理功能邊界、角色與驗收條件。", "Backend")],
            [new EngineerAssignment("Backend", "確認需求範圍")],
            [
                new WorkItemDraft(
                    "確認需求範圍",
                    $"請依照原始材料重新確認與 {topic} 相關的功能目標、角色與限制條件。",
                    ["列出核心功能", "列出角色與權限", "列出驗收條件"],
                    "Backend")
            ]);
    }

    private sealed class IngestSourceResponse
    {
        public string? Title { get; init; }
        public string? Summary { get; init; }
        public List<string>? Keywords { get; init; }
        public List<string>? Decisions { get; init; }
        public List<TaskDto>? Tasks { get; init; }
        public List<AssignmentDto>? Assignments { get; init; }
        public List<WorkItemDto>? WorkItems { get; init; }
    }

    private sealed class WorkItemRevisionResponse
    {
        public string? UpdatedDescription { get; init; }
        public List<string>? AcceptanceCriteria { get; init; }
        public List<string>? DiscussionNotes { get; init; }
        public string? FinalizedDescription { get; init; }
        public string? SuggestedEngineer { get; init; }
        public string? Status { get; init; }
    }

    private sealed class FinalizeSourceResponse
    {
        public string? FormalizedOutput { get; init; }
    }

    private sealed class TaskDto
    {
        public string? Title { get; init; }
        public string? Description { get; init; }
        public string? SuggestedEngineer { get; init; }
    }

    private sealed class AssignmentDto
    {
        public string? Engineer { get; init; }
        public string? TaskTitle { get; init; }
    }

    private sealed class WorkItemDto
    {
        public string? Title { get; init; }
        public string? Description { get; init; }
        public List<string>? AcceptanceCriteria { get; init; }
        public string? SuggestedEngineer { get; init; }
    }
}
