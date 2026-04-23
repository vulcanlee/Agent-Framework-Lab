using AgentFrameworkPersistenceMemory.Memory;

namespace AgentFrameworkPersistenceMemory.Recall;

public sealed class MemoryRecallService(IMemoryRelevanceEvaluator evaluator)
{
    private static readonly char[] TokenSeparators = [' ', '\t', '\r', '\n', '，', ',', '。', '、', '：', ':', '；', ';', '的', '與', '及'];
    private readonly IMemoryRelevanceEvaluator _evaluator = evaluator;

    public async Task<IReadOnlyList<PersistentMemoryRecord>> RecallRelevantMemoriesAsync(
        string issueText,
        IReadOnlyList<PersistentMemoryRecord> memories,
        CancellationToken cancellationToken)
    {
        var candidates = SelectCandidates(issueText, memories);
        if (candidates.Count == 0)
        {
            return [];
        }

        var relevantIds = await _evaluator.SelectRelevantMemoryIdsAsync(issueText, candidates, cancellationToken);
        var idSet = relevantIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return candidates.Where(memory => idSet.Contains(memory.Id)).ToList();
    }

    private static List<PersistentMemoryRecord> SelectCandidates(string issueText, IReadOnlyList<PersistentMemoryRecord> memories)
    {
        var normalized = issueText.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return [];
        }

        return memories
            .Where(memory =>
                memory.Keywords.Any(keyword => normalized.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ||
                normalized.Contains(memory.Title, StringComparison.OrdinalIgnoreCase) ||
                ContainsMeaningfulOverlap(normalized, memory.Title) ||
                ContainsMeaningfulOverlap(normalized, memory.Summary))
            .ToList();
    }

    private static bool ContainsMeaningfulOverlap(string issueText, string sourceText)
    {
        var tokens = sourceText.Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.Any(token => token.Length >= 2 && issueText.Contains(token, StringComparison.OrdinalIgnoreCase));
    }
}
