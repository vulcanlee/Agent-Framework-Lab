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
        await _output.WriteLineAsync("輸入 /help 查看可用指令，也可以直接輸入自然語言進行討論。");

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

            var discussion = await _workflow.DiscussAsync(trimmed, cancellationToken);
            await _output.WriteLineAsync(discussion);
        }
        catch (UserFacingException ex)
        {
            await _output.WriteLineAsync(ex.Message);
        }
    }

    private async Task HandleCommandAsync(string commandLine, CancellationToken cancellationToken)
    {
        if (commandLine.StartsWith("/ingest ", StringComparison.OrdinalIgnoreCase))
        {
            var filePath = commandLine["/ingest".Length..].Trim();
            await IngestFromFileAsync(filePath, cancellationToken);
            return;
        }

        if (commandLine.StartsWith("/work add ", StringComparison.OrdinalIgnoreCase))
        {
            await HandleWorkAddCommandAsync(commandLine, cancellationToken);
            return;
        }

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
            case "/source":
                await HandleSourceCommandAsync(parts, cancellationToken);
                return;
            case "/finalize":
                await HandleFinalizeCommandAsync(commandLine, parts, cancellationToken);
                return;
            default:
                await _output.WriteLineAsync("未知指令，請輸入 /help 查看可用功能。");
                return;
        }
    }

    private async Task HandleSourceCommandAsync(string[] parts, CancellationToken cancellationToken)
    {
        if (parts.Length < 3 || !string.Equals(parts[1], "remove", StringComparison.OrdinalIgnoreCase))
        {
            await _output.WriteLineAsync("用法：/source remove <source-id>");
            return;
        }

        await _workflow.RemoveSourceAsync(parts[2], cancellationToken);
        await _output.WriteLineAsync($"已移除來源 {parts[2]}。");
    }

    private async Task HandleWorkCommandAsync(string[] parts, CancellationToken cancellationToken)
    {
        if (parts.Length < 2)
        {
            await _output.WriteLineAsync("用法：/work list [source-id]、/work review <work-id>、/work update <work-id> <修正內容>、/work remove <work-id>、/work add <title> --desc <description> [--owner <engineer>] [--accept <item1;item2>]");
            return;
        }

        switch (parts[1].ToLowerInvariant())
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

                var sourceId = RequireActiveSource();
                var activeSource = await _workflow.GetSourceAsync(sourceId, cancellationToken);
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

                var sourceId = RequireActiveSource();
                var updated = await _workflow.ReviseWorkItemAsync(sourceId, parts[2], parts[3], cancellationToken);
                _sessionManager.SetActiveWorkItem(updated.Id);
                await _output.WriteLineAsync($"已更新 {updated.Id}：{updated.CurrentDescription}");
                return;
            }
            case "remove":
            {
                if (parts.Length < 3)
                {
                    await _output.WriteLineAsync("用法：/work remove <work-id>");
                    return;
                }

                var sourceId = RequireActiveSource();
                await _workflow.RemoveWorkItemAsync(sourceId, parts[2], cancellationToken);
                await _output.WriteLineAsync($"已移除工作項目 {parts[2]}。");
                return;
            }
            default:
                await _output.WriteLineAsync("用法：/work list [source-id]、/work review <work-id>、/work update <work-id> <修正內容>、/work remove <work-id>、/work add <title> --desc <description> [--owner <engineer>] [--accept <item1;item2>]");
                return;
        }
    }

    private async Task HandleWorkAddCommandAsync(string commandLine, CancellationToken cancellationToken)
    {
        var sourceId = RequireActiveSource();
        var spec = commandLine["/work add".Length..].Trim();

        var descIndex = spec.IndexOf(" --desc ", StringComparison.OrdinalIgnoreCase);
        if (descIndex <= 0)
        {
            await _output.WriteLineAsync("用法：/work add <title> --desc <description> [--owner <engineer>] [--accept <item1;item2>]");
            return;
        }

        var title = spec[..descIndex].Trim();
        var optionText = spec[descIndex..];
        var description = ExtractOption(optionText, "--desc");
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
        {
            await _output.WriteLineAsync("新增工作項目時，title 與 --desc 都是必填。");
            return;
        }

        var owner = ExtractOption(optionText, "--owner");
        var accept = ExtractOption(optionText, "--accept");
        var acceptanceCriteria = string.IsNullOrWhiteSpace(accept)
            ? []
            : accept.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var workItem = await _workflow.AddWorkItemAsync(
            sourceId,
            new ManualWorkItemDraft(title, description, string.IsNullOrWhiteSpace(owner) ? "Backend" : owner, acceptanceCriteria),
            cancellationToken);

        _sessionManager.SetActiveWorkItem(workItem.Id);
        await _output.WriteLineAsync($"已新增工作項目 {workItem.Id}：{workItem.Title}");
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

    private async Task IngestFromFileAsync(string filePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            await _output.WriteLineAsync("請提供文字檔路徑，例如 /ingest docs/sample.txt");
            return;
        }

        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
        {
            await _output.WriteLineAsync($"找不到檔案 {filePath}。");
            return;
        }

        string rawInput;
        try
        {
            rawInput = await File.ReadAllTextAsync(fullPath, new UTF8Encoding(false), cancellationToken);
        }
        catch (Exception ex)
        {
            await _output.WriteLineAsync($"讀取檔案失敗：{ex.Message}");
            return;
        }

        if (string.IsNullOrWhiteSpace(rawInput))
        {
            await _output.WriteLineAsync("指定的文字檔沒有內容。");
            return;
        }

        _sessionManager.CancelIngestMode();
        var source = await _workflow.IngestSourceAsync(NormalizeInput(rawInput), cancellationToken);
        _sessionManager.SetActiveSource(source.Id);
        await _output.WriteLineAsync($"已從檔案建立來源 {source.Id}：{source.Title}");
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

    private string RequireActiveSource()
    {
        if (string.IsNullOrWhiteSpace(_sessionManager.Current.ActiveSourceId))
        {
            throw new UserFacingException("請先使用 /work list <source-id> 載入來源。");
        }

        return _sessionManager.Current.ActiveSourceId!;
    }

    private static string NormalizeInput(string input)
        => input.Trim().TrimStart('\uFEFF').Normalize(NormalizationForm.FormC);

    private static string? FindWorkItemId(string text)
    {
        var match = WorkItemRegex().Match(text);
        return match.Success ? match.Value.ToUpperInvariant() : null;
    }

    private static string? ExtractOption(string text, string optionName)
    {
        var optionStart = text.IndexOf(optionName, StringComparison.OrdinalIgnoreCase);
        if (optionStart < 0)
        {
            return null;
        }

        var valueStart = optionStart + optionName.Length;
        while (valueStart < text.Length && char.IsWhiteSpace(text[valueStart]))
        {
            valueStart++;
        }

        var nextIndexes = new[]
        {
            text.IndexOf(" --desc ", valueStart, StringComparison.OrdinalIgnoreCase),
            text.IndexOf(" --owner ", valueStart, StringComparison.OrdinalIgnoreCase),
            text.IndexOf(" --accept ", valueStart, StringComparison.OrdinalIgnoreCase)
        }.Where(index => index >= 0 && index > optionStart).ToList();

        var valueEnd = nextIndexes.Count == 0 ? text.Length : nextIndexes.Min();
        return text[valueStart..valueEnd].Trim();
    }

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

            /ingest
            進入貼上模式，準備貼入原始需求

            /ingest <text-file-path>
            從 UTF-8 文字檔讀入原始需求並直接分析

            /end
            結束貼上模式並開始拆解工作項目

            /source remove <source-id>
            硬刪除來源與其全部 work item

            /work list [source-id]
            列出指定來源的工作項目

            /work review <work-id>
            檢視指定工作項目的目前版本與驗收條件

            /work update <work-id> <修正內容>
            直接更新指定工作項目的描述與驗收方向

            /work remove <work-id>
            移除指定工作項目

            /work add <title> --desc <description> [--owner <engineer>] [--accept <item1;item2>]
            手動新增工作項目

            /finalize <source-id> [--save <path>]
            產出正式工作需求清單，並可另存檔案

            /show-session
            顯示目前 session 摘要

            /show-memory
            顯示永久記憶中的來源列表

            /new-session
            建立新的空白 session

            聊天模式
            直接輸入自然語言會進入聊天模式。
            若目前有 active source 或 active work item，聊天會自動帶入上下文。

            範例
            W1 的驗收條件還缺什麼？
            幫我比較 W2 與 W3 哪個應該先做
            這個需求還有哪些風險
            """;
    }
}
