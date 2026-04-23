using System.Text;
using AgentFrameworkPersistenceMemory.Agent;
using AgentFrameworkPersistenceMemory.Memory;
using AgentFrameworkPersistenceMemory.Recall;
using AgentFrameworkPersistenceMemory.Sessions;
using AgentFrameworkPersistenceMemory.Status;

namespace AgentFrameworkPersistenceMemory.Workflow;

public sealed class PmWorkflow(
    PersistentMemoryStore memoryStore,
    SessionManager sessionManager,
    MemoryRecallService recallService,
    IPmAgentService pmAgentService,
    AgentStatusReporter statusReporter,
    EngineerRoster engineerRoster) : IPmWorkflow
{
    private readonly PersistentMemoryStore _memoryStore = memoryStore;
    private readonly SessionManager _sessionManager = sessionManager;
    private readonly MemoryRecallService _recallService = recallService;
    private readonly IPmAgentService _pmAgentService = pmAgentService;
    private readonly AgentStatusReporter _statusReporter = statusReporter;
    private readonly EngineerRoster _engineerRoster = engineerRoster;

    public async Task<PersistentMemoryRecord> IngestSourceAsync(string rawInput, CancellationToken cancellationToken)
    {
        _sessionManager.AddUserInput(rawInput);
        _statusReporter.Report("正在檢查目前 session 上下文");
        var sessionSummary = _sessionManager.GetSummary();

        _statusReporter.Report("正在搜尋可能相關的永久記憶");
        var allMemories = await _memoryStore.LoadAsync(cancellationToken);

        _statusReporter.Report("正在判斷哪些歷史需求與本次問題相關");
        var recalled = await _recallService.RecallRelevantMemoriesAsync(rawInput, allMemories, cancellationToken);
        foreach (var memory in recalled)
        {
            _sessionManager.AddRecalledMemory(memory.Id);
        }

        _statusReporter.Report("正在整理原始需求並拆解工作項目");
        var result = await _pmAgentService.IngestSourceAsync(
            new IngestSourceRequest(rawInput, sessionSummary, recalled, _engineerRoster),
            cancellationToken);

        var nextSourceId = BuildNextSourceId(allMemories);
        var workItems = result.WorkItems
            .Select((item, index) => new WorkItemRecord(
                $"W{index + 1}",
                item.Title,
                item.Description,
                item.Description,
                [],
                [],
                item.AcceptanceCriteria,
                item.SuggestedEngineer,
                item.Description,
                "draft"))
            .ToList();

        var record = new PersistentMemoryRecord(
            nextSourceId,
            result.Title,
            result.Summary,
            rawInput,
            result.Keywords,
            result.Decisions,
            result.Tasks,
            result.Assignments,
            workItems,
            null,
            DateTimeOffset.UtcNow);

        _statusReporter.Report("正在寫回永久記憶");
        await _memoryStore.UpsertAsync(record, cancellationToken);

        _sessionManager.SetActiveSource(record.Id);
        _sessionManager.ClearActiveWorkItem();
        _sessionManager.AddAgentNote(result.Summary);
        return record;
    }

    public async Task<string> DiscussAsync(string userMessage, CancellationToken cancellationToken)
    {
        _sessionManager.AddUserInput(userMessage);
        _statusReporter.Report("正在整理聊天上下文");

        var allMemories = await _memoryStore.LoadAsync(cancellationToken);
        var recalled = allMemories
            .Where(memory => _sessionManager.Current.RecalledMemoryIds.Contains(memory.Id, StringComparer.OrdinalIgnoreCase))
            .ToList();

        PersistentMemoryRecord? activeSource = null;
        WorkItemRecord? activeWorkItem = null;

        if (!string.IsNullOrWhiteSpace(_sessionManager.Current.ActiveSourceId))
        {
            activeSource = allMemories.FirstOrDefault(memory =>
                string.Equals(memory.Id, _sessionManager.Current.ActiveSourceId, StringComparison.OrdinalIgnoreCase));
        }

        if (activeSource is not null && !string.IsNullOrWhiteSpace(_sessionManager.Current.ActiveWorkItemId))
        {
            activeWorkItem = activeSource.WorkItems.FirstOrDefault(item =>
                string.Equals(item.Id, _sessionManager.Current.ActiveWorkItemId, StringComparison.OrdinalIgnoreCase));
        }

        _statusReporter.Report("正在進行需求討論與分析");
        var discussion = await _pmAgentService.DiscussAsync(
            new DiscussionRequest(
                userMessage,
                _sessionManager.GetSummary(),
                activeSource,
                activeWorkItem,
                recalled),
            cancellationToken);

        _sessionManager.AddAgentNote(discussion.Reply);
        if (discussion.ReferencedWorkItemIds.Count == 1)
        {
            _sessionManager.SetActiveWorkItem(discussion.ReferencedWorkItemIds[0]);
        }

        return BuildDiscussionOutput(discussion);
    }

    public async Task<WorkItemRecord> ReviseWorkItemAsync(string sourceId, string workItemId, string feedback, CancellationToken cancellationToken)
    {
        var record = await GetSourceAsync(sourceId, cancellationToken);
        var workItem = record.WorkItems.FirstOrDefault(item => string.Equals(item.Id, workItemId, StringComparison.OrdinalIgnoreCase))
            ?? throw new UserFacingException($"找不到工作項目 {workItemId}。");

        _statusReporter.Report($"正在檢討並修正工作項目 {workItemId}");
        var result = await _pmAgentService.ReviseWorkItemAsync(
            new WorkItemRevisionRequest(record, workItem, feedback, _sessionManager.GetSummary()),
            cancellationToken);

        var updatedWorkItem = workItem with
        {
            CurrentDescription = result.UpdatedDescription,
            DiscussionNotes = workItem.DiscussionNotes.Concat(result.DiscussionNotes).ToList(),
            RevisionSuggestions = workItem.RevisionSuggestions.Concat([feedback]).ToList(),
            AcceptanceCriteria = result.AcceptanceCriteria,
            SuggestedEngineer = result.SuggestedEngineer,
            FinalizedDescription = result.FinalizedDescription,
            Status = result.Status
        };

        var updatedRecord = record with
        {
            WorkItems = record.WorkItems.Select(item => item.Id == updatedWorkItem.Id ? updatedWorkItem : item).ToList(),
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _statusReporter.Report("正在寫回永久記憶");
        await _memoryStore.UpsertAsync(updatedRecord, cancellationToken);

        _sessionManager.SetActiveSource(sourceId);
        _sessionManager.SetActiveWorkItem(updatedWorkItem.Id);
        _sessionManager.AddAgentNote($"已更新 {updatedWorkItem.Id}");
        return updatedWorkItem;
    }

    public async Task<WorkItemRecord> AddWorkItemAsync(string sourceId, ManualWorkItemDraft draft, CancellationToken cancellationToken)
    {
        var record = await GetSourceAsync(sourceId, cancellationToken);
        var nextId = BuildNextWorkItemId(record.WorkItems);
        var workItem = new WorkItemRecord(
            nextId,
            draft.Title,
            draft.Description,
            draft.Description,
            [],
            [],
            draft.AcceptanceCriteria,
            draft.SuggestedEngineer,
            draft.Description,
            "draft");

        var updatedRecord = record with
        {
            WorkItems = record.WorkItems.Concat([workItem]).ToList(),
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _statusReporter.Report($"正在新增工作項目 {nextId}");
        await _memoryStore.UpsertAsync(updatedRecord, cancellationToken);

        _sessionManager.SetActiveSource(sourceId);
        _sessionManager.SetActiveWorkItem(nextId);
        _sessionManager.AddAgentNote($"已新增 {nextId}");
        return workItem;
    }

    public async Task RemoveWorkItemAsync(string sourceId, string workItemId, CancellationToken cancellationToken)
    {
        var record = await GetSourceAsync(sourceId, cancellationToken);
        var removed = record.WorkItems.Any(item => string.Equals(item.Id, workItemId, StringComparison.OrdinalIgnoreCase));
        if (!removed)
        {
            throw new UserFacingException($"找不到工作項目 {workItemId}。");
        }

        var remainingItems = record.WorkItems
            .Where(item => !string.Equals(item.Id, workItemId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        _statusReporter.Report($"正在移除工作項目 {workItemId}");
        await _memoryStore.ReplaceWorkItemsAsync(sourceId, remainingItems, cancellationToken);

        _sessionManager.SetActiveSource(sourceId);
        if (string.Equals(_sessionManager.Current.ActiveWorkItemId, workItemId, StringComparison.OrdinalIgnoreCase))
        {
            _sessionManager.ClearActiveWorkItem();
        }
    }

    public async Task RemoveSourceAsync(string sourceId, CancellationToken cancellationToken)
    {
        _ = await GetSourceAsync(sourceId, cancellationToken);
        _statusReporter.Report($"正在移除來源 {sourceId}");
        await _memoryStore.RemoveAsync(sourceId, cancellationToken);

        if (string.Equals(_sessionManager.Current.ActiveSourceId, sourceId, StringComparison.OrdinalIgnoreCase))
        {
            _sessionManager.ClearActiveSource();
        }
    }

    public async Task<string> FinalizeSourceAsync(string sourceId, string? savePath, CancellationToken cancellationToken)
    {
        var record = await GetSourceAsync(sourceId, cancellationToken);
        _statusReporter.Report("正在彙整正式工作需求清單");
        var result = await _pmAgentService.FinalizeSourceAsync(
            new FinalizeSourceRequest(record, _sessionManager.GetSummary()),
            cancellationToken);

        var updatedRecord = record with
        {
            WorkItems = record.WorkItems.Select(item => item with
            {
                Status = "finalized",
                FinalizedDescription = string.IsNullOrWhiteSpace(item.FinalizedDescription) ? item.CurrentDescription : item.FinalizedDescription
            }).ToList(),
            FinalizedOutput = result.FormalizedOutput,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _statusReporter.Report("正在寫回永久記憶");
        await _memoryStore.UpsertAsync(updatedRecord, cancellationToken);

        if (!string.IsNullOrWhiteSpace(savePath))
        {
            var fullPath = Path.GetFullPath(savePath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(fullPath, result.FormalizedOutput, new UTF8Encoding(false), cancellationToken);
        }

        _sessionManager.SetActiveSource(sourceId);
        _sessionManager.AddAgentNote($"已完成正式清單輸出 {sourceId}");
        return result.FormalizedOutput;
    }

    public async Task<PersistentMemoryRecord> GetSourceAsync(string sourceId, CancellationToken cancellationToken)
    {
        var memories = await _memoryStore.LoadAsync(cancellationToken);
        return memories.FirstOrDefault(memory => string.Equals(memory.Id, sourceId, StringComparison.OrdinalIgnoreCase))
            ?? throw new UserFacingException($"找不到來源 {sourceId}。");
    }

    public Task<IReadOnlyList<PersistentMemoryRecord>> LoadMemoryAsync(CancellationToken cancellationToken)
        => _memoryStore.LoadAsync(cancellationToken);

    private static string BuildNextSourceId(IReadOnlyList<PersistentMemoryRecord> memories)
    {
        var nextNumber = memories
            .Select(memory => memory.Id)
            .Where(id => id.StartsWith("source-", StringComparison.OrdinalIgnoreCase))
            .Select(id => id["source-".Length..])
            .Select(id => int.TryParse(id, out var number) ? number : 0)
            .DefaultIfEmpty()
            .Max() + 1;

        return $"source-{nextNumber:000}";
    }

    private static string BuildNextWorkItemId(IReadOnlyList<WorkItemRecord> workItems)
    {
        var nextNumber = workItems
            .Select(item => item.Id)
            .Where(id => id.StartsWith("W", StringComparison.OrdinalIgnoreCase))
            .Select(id => id[1..])
            .Select(id => int.TryParse(id, out var number) ? number : 0)
            .DefaultIfEmpty()
            .Max() + 1;

        return $"W{nextNumber}";
    }

    private static string BuildDiscussionOutput(DiscussionResult discussion)
    {
        var builder = new StringBuilder();
        builder.AppendLine(discussion.Reply);

        if (discussion.SuggestedNextActions.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("建議下一步：");
            foreach (var action in discussion.SuggestedNextActions)
            {
                builder.AppendLine($"- {action}");
            }
        }

        return builder.ToString().TrimEnd();
    }
}
