using Microsoft.Extensions.AI;

internal sealed record FileInput(
    string FilePath,
    string FileName,
    string MediaType,
    bool IsImage,
    IReadOnlyList<AIContent> PromptContents);
