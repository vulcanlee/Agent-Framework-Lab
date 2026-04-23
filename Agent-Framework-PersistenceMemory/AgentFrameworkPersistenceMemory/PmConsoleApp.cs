using System.Text;
using System.Text.RegularExpressions;
using AgentFrameworkPersistenceMemory.Memory;
using AgentFrameworkPersistenceMemory.Sessions;
using AgentFrameworkPersistenceMemory.Workflow;

namespace AgentFrameworkPersistenceMemory;

public sealed partial class PmConsoleApp(
    PersistentMemoryStore memoryStore,
    SessionManager sessionManager,
    IPmWorkflow workflow,
    TextWriter output,
    TextReader? input = null)
{
    private readonly PersistentMemoryStore _memoryStore = memoryStore;
    private readonly SessionManager _sessionManager = sessionManager;
    private readonly IPmWorkflow _workflow = workflow;
    private readonly TextWriter _output = output;
    private readonly TextReader _input = input ?? Console.In;

    [GeneratedRegex(@"\bW\d+\b", RegexOptions.IgnoreCase)]
    private static partial Regex WorkItemRegex();

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await _output.WriteLineAsync("極簡 PM Agent with Persistent Memory");
        await _output.WriteLineAsync("輸入 /help 查看可用指令。");

        while (!cancellationToken.IsCancellationRequested)
        {
            await _output.WriteAsync("> ");
            var line = await _input.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            await HandleInputAsync(line, cancellationToken);
        }
    }

    public async Task HandleInputAsync(string line, CancellationToken cancellationToken)
    {
        var trimmed = NormalizeInput(line);
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return;
        }

        try
        {
            if (_sessionManager.Current.IsIngestMode && !trimmed.StartsWith('/'))
            {
                _sessionManager.AppendIngestLine(trimmed);
                return;
            }

            if (trimmed.StartsWith('/'))
            {
                await HandleCommandAsync(trimmed, cancellationToken);
                return;
            }

            var workItemId = FindWorkItemId(trimmed) ?? _sessionManager.Current.ActiveWorkItemId;
            var sourceId = _sessionManager.Current.ActiveSourceId;
            if (sourceId is not null && workItemId is not null)
            {
                var updated = await _workflow.ReviseWorkItemAsync(sourceId, workItemId, trimmed, cancellationToken);
                _sessionManager.SetActiveWorkItem(updated.Id);
                await _output.WriteLineAsync($"已更新 {updated.Id}：{updated.CurrentDescription}");
                return;
            }

            await _output.WriteLineAsync("請先使用 /ingest 匯入需求，或先用 /work review <work-id> 選定要討論的工作項目。");
        }
        catch (UserFacingException ex)
        {
            await _output.WriteLineAsync(ex.Message);
        }
    }

    private async Task HandleCommandAsync(string commandLine, CancellationToken cancellationToken)
    {
        var parts = commandLine.Split(' ', 4, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var command = parts[0];

        switch (command)
        {
            case "/help":
                await _output.WriteLineAsync(BuildHelpText());
                return;
            case "/new-session":
                _sessionManager.NewSession();
                await _output.WriteLineAsync("已建立新的 session。");
                return;
            case "/show-session":
                await _output.WriteLineAsync(_sessionManager.GetSummary());
                return;
            case "/show-memory":
                await ShowMemoryAsync(cancellationToken);
                return;
            case "/ingest":
                _sessionManager.StartIngestMode();
                await _output.WriteLineAsync("已進入貼上模式。請貼上原始需求內容，完成後輸入 /end。");
                return;
            case "/end":
                await CompleteIngestAsync(cancellationToken);
                return;
            case "/work":
                await HandleWorkCommandAsync(parts, cancellationToken);
                return;
            case "/finalize":
                await HandleFinalizeCommandAsync(commandLine, parts, cancellationToken);
                return;
            default:
                await _output.WriteLineAsync("未知指令，請輸入 /help 查看可用功能。");
                return;
        }
    }

    private async Task HandleWorkCommandAsync(string[] parts, CancellationToken cancellationToken)
    {
        if (parts.Length < 2)
        {
            await _output.WriteLineAsync("用法：/work list [source-id]、/work review <work-id>、/work update <work-id> <修正內容>");
            return;
        }

        switch (parts[1])
        {
            case "list":
            {
                var sourceId = parts.Length >= 3 ? parts[2] : _sessionManager.Current.ActiveSourceId;
                if (string.IsNullOrWhiteSpace(sourceId))
                {
                    await _output.WriteLineAsync("請提供 source-id，例如 /work list source-001。");
                    return;
                }

                var source = await _workflow.GetSourceAsync(sourceId, cancellationToken);
                _sessionManager.SetActiveSource(source.Id);
                await _output.WriteLineAsync(FormatWorkList(source));
                return;
            }
            case "review":
            {
                if (parts.Length < 3)
                {
                    await _output.WriteLineAsync("用法：/work review <work-id>");
                    return;
                }

                if (string.IsNullOrWhiteSpace(_sessionManager.Current.ActiveSourceId))
                {
                    await _output.WriteLineAsync("請先使用 /work list <source-id> 載入來源。");
                    return;
                }

                var activeSource = await _workflow.GetSourceAsync(_sessionManager.Current.ActiveSourceId!, cancellationToken);
                var workItem = activeSource.WorkItems.FirstOrDefault(item => string.Equals(item.Id, parts[2], StringComparison.OrdinalIgnoreCase));
                if (workItem is null)
                {
                    await _output.WriteLineAsync($"找不到工作項目 {parts[2]}。");
                    return;
                }

                _sessionManager.SetActiveWorkItem(workItem.Id);
                await _output.WriteLineAsync(FormatWorkItem(workItem));
                return;
            }
            case "update":
            {
                if (parts.Length < 4)
                {
                    await _output.WriteLineAsync("用法：/work update <work-id> <修正內容>");
                    return;
                }

                if (string.IsNullOrWhiteSpace(_sessionManager.Current.ActiveSourceId))
                {
                    await _output.WriteLineAsync("請先使用 /work list <source-id> 載入來源。");
                    return;
                }

                var updated = await _workflow.ReviseWorkItemAsync(_sessionManager.Current.ActiveSourceId!, parts[2], parts[3], cancellationToken);
                _sessionManager.SetActiveWorkItem(updated.Id);
                await _output.WriteLineAsync($"已更新 {updated.Id}：{updated.CurrentDescription}");
                return;
            }
            default:
                await _output.WriteLineAsync("用法：/work list [source-id]、/work review <work-id>、/work update <work-id> <修正內容>");
                return;
        }
    }

    private async Task HandleFinalizeCommandAsync(string commandLine, string[] parts, CancellationToken cancellationToken)
    {
        if (parts.Length < 2)
        {
            await _output.WriteLineAsync("用法：/finalize <source-id> [--save <path>]");
            return;
        }

        string? savePath = null;
        var saveIndex = commandLine.IndexOf("--save", StringComparison.OrdinalIgnoreCase);
        if (saveIndex >= 0)
        {
            savePath = commandLine[(saveIndex + "--save".Length)..].Trim();
        }

        var output = await _workflow.FinalizeSourceAsync(parts[1], string.IsNullOrWhiteSpace(savePath) ? null : savePath, cancellationToken);
        await _output.WriteLineAsync(output);
    }

    private async Task CompleteIngestAsync(CancellationToken cancellationToken)
    {
        if (!_sessionManager.Current.IsIngestMode)
        {
            await _output.WriteLineAsync("目前不在貼上模式，請先輸入 /ingest。");
            return;
        }

        var rawInput = _sessionManager.CompleteIngest();
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            await _output.WriteLineAsync("沒有收到任何原始需求內容。");
            return;
        }

        var source = await _workflow.IngestSourceAsync(rawInput, cancellationToken);
        _sessionManager.SetActiveSource(source.Id);
        await _output.WriteLineAsync($"已建立來源 {source.Id}：{source.Title}");
        await _output.WriteLineAsync($"已拆解出 {source.WorkItems.Count} 個工作項目。");
        await _output.WriteLineAsync(FormatWorkList(source));
    }

    private async Task ShowMemoryAsync(CancellationToken cancellationToken)
    {
        var memories = await _workflow.LoadMemoryAsync(cancellationToken);
        if (memories.Count == 0)
        {
            await _output.WriteLineAsync("目前沒有任何永久記憶。");
            return;
        }

        foreach (var memory in memories.OrderByDescending(memory => memory.UpdatedAt))
        {
            await _output.WriteLineAsync($"- {memory.Id} | {memory.Title} | {memory.UpdatedAt:yyyy-MM-dd HH:mm:ss}");
        }
    }

    private static string? FindWorkItemId(string text)
    {
        var match = WorkItemRegex().Match(text);
        return match.Success ? match.Value.ToUpperInvariant() : null;
    }

    private static string NormalizeInput(string input)
        => input.Trim().TrimStart('\uFEFF').Normalize(NormalizationForm.FormC);

    private static string FormatWorkList(PersistentMemoryRecord source)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"來源：{source.Id} - {source.Title}");
        foreach (var item in source.WorkItems)
        {
            builder.AppendLine($"- {item.Id} | {item.Title} | {item.CurrentDescription} | {item.Status}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string FormatWorkItem(WorkItemRecord item)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{item.Id} - {item.Title}");
        builder.AppendLine($"目前版本：{item.CurrentDescription}");
        builder.AppendLine($"驗收重點：{string.Join("、", item.AcceptanceCriteria)}");
        builder.AppendLine($"建議指派：{item.SuggestedEngineer}");
        builder.AppendLine($"狀態：{item.Status}");
        return builder.ToString().TrimEnd();
    }

    private static string BuildHelpText()
    {
        return """
            可用指令

            /help
            查看所有功能與簡例
            範例：/help

            /ingest
            進入貼上模式，準備貼入原始需求
            範例：/ingest

            /end
            結束貼上模式並開始拆解工作項目
            範例：/end

            /work list [source-id]
            列出指定來源的工作項目
            範例：/work list source-001

            /work review <work-id>
            檢視指定工作項目的目前版本與驗收條件
            範例：/work review W1

            /work update <work-id> <修正內容>
            直接更新指定工作項目的描述與驗收方向
            範例：/work update W1 加入 email 驗證與通知機制

            /finalize <source-id> [--save <path>]
            產出正式工作需求清單，並可另存檔案
            範例：/finalize source-001 --save docs/formal-requirements/source-001.md

            /show-session
            顯示目前 session 摘要
            範例：/show-session

            /show-memory
            顯示永久記憶中的來源列表
            範例：/show-memory

            /new-session
            建立新的空白 session
            範例：/new-session

            推薦操作流程
            1. /ingest
            2. 貼上原始需求內容
            3. /end
            4. /work review W1 後，再用自然語言或 /work update 繼續修正
            5. /finalize source-001
            """;
    }
}
