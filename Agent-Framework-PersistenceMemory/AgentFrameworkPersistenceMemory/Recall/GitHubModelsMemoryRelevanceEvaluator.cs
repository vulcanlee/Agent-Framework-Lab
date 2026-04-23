using System.Text.Json;
using AgentFrameworkPersistenceMemory.Infrastructure;
using AgentFrameworkPersistenceMemory.Memory;

namespace AgentFrameworkPersistenceMemory.Recall;

public sealed class GitHubModelsMemoryRelevanceEvaluator(IGitHubModelsClient client) : IMemoryRelevanceEvaluator
{
    private readonly IGitHubModelsClient _client = client;

    public async Task<IReadOnlyList<string>> SelectRelevantMemoryIdsAsync(
        string issueText,
        IReadOnlyList<PersistentMemoryRecord> candidates,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        var systemPrompt = """
            你是需求記憶檢索助理。
            你的任務是從候選永久記憶中選出與本次需求最相關的記憶 id。
            只回傳 JSON，格式必須是：
            {
              "relevantIds": ["id1", "id2"]
            }
            如果沒有相關記憶，請回傳空陣列。
            """;

        var candidateText = string.Join(Environment.NewLine, candidates.Select(candidate =>
            $"- id: {candidate.Id} | title: {candidate.Title} | keywords: {string.Join(", ", candidate.Keywords)} | summary: {candidate.Summary}"));

        var userPrompt = $"""
            本次需求：
            {issueText}

            候選永久記憶：
            {candidateText}
            """;

        var content = await _client.CompleteAsync(systemPrompt, userPrompt, cancellationToken);
        var response = JsonSerializer.Deserialize<RelevantMemoryResponse>(content, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return response?.RelevantIds ?? [];
    }

    private sealed class RelevantMemoryResponse
    {
        public List<string> RelevantIds { get; init; } = [];
    }
}
