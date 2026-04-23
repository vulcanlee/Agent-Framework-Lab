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
        _sessionManager.SetActiveWorkItem(workItemId);
        _sessionManager.AddAgentNote($"已更新 {workItemId}");
        return updatedWorkItem;
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
}
