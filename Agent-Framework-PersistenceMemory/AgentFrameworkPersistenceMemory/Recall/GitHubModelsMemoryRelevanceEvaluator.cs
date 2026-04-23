using System.Text.Json;
using AgentFrameworkPersistenceMemory.Infrastructure;
using AgentFrameworkPersistenceMemory.Memory;
using AgentFrameworkPersistenceMemory.Status;

namespace AgentFrameworkPersistenceMemory.Recall;

public sealed class GitHubModelsMemoryRelevanceEvaluator(
    IGitHubModelsClient client,
    AgentStatusReporter statusReporter) : IMemoryRelevanceEvaluator
{
    private readonly IGitHubModelsClient _client = client;
    private readonly AgentStatusReporter _statusReporter = statusReporter;

    public async Task<IReadOnlyList<string>> SelectRelevantMemoryIdsAsync(
        string issueText,
        IReadOnlyList<PersistentMemoryRecord> candidates,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        const string systemPrompt = """
你是記憶相關性判斷助手。
請從候選永久記憶中找出與目前問題真正相關的項目，只輸出 JSON：
{
  "relevantIds": ["id1", "id2"]
}
如果沒有相關項目，就回傳空陣列。
""";

        var candidateText = string.Join(Environment.NewLine, candidates.Select(candidate =>
            $"- id: {candidate.Id} | title: {candidate.Title} | keywords: {string.Join(", ", candidate.Keywords)} | summary: {candidate.Summary}"));

        var userPrompt = $"""
目前問題：
{issueText}

候選永久記憶：
{candidateText}
""";

        var completion = await _client.CompleteAsync(systemPrompt, userPrompt, cancellationToken);
        _statusReporter.ReportTokenUsage(completion.Usage);

        var response = JsonSerializer.Deserialize<RelevantMemoryResponse>(completion.Content, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return response?.RelevantIds ?? [];
    }

    private sealed class RelevantMemoryResponse
    {
        public List<string> RelevantIds { get; init; } = [];
    }
}
