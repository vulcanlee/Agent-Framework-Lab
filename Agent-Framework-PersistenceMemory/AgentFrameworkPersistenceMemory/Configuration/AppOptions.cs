namespace AgentFrameworkPersistenceMemory.Configuration;

public sealed class AppOptions
{
    public GitHubModelsOptions GitHubModels { get; init; } = new();

    public MemoryOptions Memory { get; init; } = new();
}

public sealed class GitHubModelsOptions
{
    public string BaseUrl { get; init; } = "https://models.github.ai/inference";

    public string Model { get; init; } = "openai/gpt-4.1";

    public string ApiVersion { get; init; } = "2026-03-10";

    public string TokenEnvironmentVariable { get; init; } = "GITHUB_TOKEN";
}

public sealed class MemoryOptions
{
    public string FilePath { get; init; } = "data/persistent-memory.json";
}
