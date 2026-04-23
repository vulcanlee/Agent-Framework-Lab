using System.Text;

namespace AgentFrameworkPersistenceMemory.Sessions;

public sealed class SessionManager
{
    public SessionState Current { get; private set; } = new();

    public void AddUserInput(string input)
    {
        if (!string.IsNullOrWhiteSpace(input))
        {
            Current.UserInputs.Add(input.Trim().Normalize(NormalizationForm.FormC));
        }
    }

    public void AddAgentNote(string note)
    {
        if (!string.IsNullOrWhiteSpace(note))
        {
            Current.AgentNotes.Add(note.Trim().Normalize(NormalizationForm.FormC));
        }
    }

    public void AddRecalledMemory(string memoryId)
    {
        if (!string.IsNullOrWhiteSpace(memoryId) && !Current.RecalledMemoryIds.Contains(memoryId, StringComparer.OrdinalIgnoreCase))
        {
            Current.RecalledMemoryIds.Add(memoryId);
        }
    }

    public void SetActiveSource(string sourceId)
    {
        Current.ActiveSourceId = sourceId;
    }

    public void ClearActiveSource()
    {
        Current.ActiveSourceId = null;
        Current.ActiveWorkItemId = null;
    }

    public void SetActiveWorkItem(string workItemId)
    {
        Current.ActiveWorkItemId = workItemId;
    }

    public void ClearActiveWorkItem()
    {
        Current.ActiveWorkItemId = null;
    }

    public void StartIngestMode()
    {
        Current.IsIngestMode = true;
        Current.IngestBuffer.Clear();
    }

    public void AppendIngestLine(string line)
    {
        if (!string.IsNullOrWhiteSpace(line))
        {
            Current.IngestBuffer.Add(line.Normalize(NormalizationForm.FormC));
        }
    }

    public string CompleteIngest()
    {
        var text = string.Join(Environment.NewLine, Current.IngestBuffer);
        Current.IsIngestMode = false;
        Current.IngestBuffer.Clear();
        return text;
    }

    public void CancelIngestMode()
    {
        Current.IsIngestMode = false;
        Current.IngestBuffer.Clear();
    }

    public void NewSession()
    {
        Current = new SessionState();
    }

    public string GetSummary()
    {
        if (Current.UserInputs.Count == 0 &&
            Current.AgentNotes.Count == 0 &&
            Current.ActiveSourceId is null &&
            Current.ActiveWorkItemId is null)
        {
            return "目前 session 還沒有任何內容。";
        }

        return $"使用者輸入 {Current.UserInputs.Count} 筆，Agent 筆記 {Current.AgentNotes.Count} 筆，回填記憶 {Current.RecalledMemoryIds.Count} 筆，啟用中的 source：{Current.ActiveSourceId ?? "無"}，啟用中的 work item：{Current.ActiveWorkItemId ?? "無"}。";
    }
}
