namespace AgentFunctionCall.Configuration;

public sealed record AppConfiguration(Uri Endpoint, string Model, string GitHubToken)
{
    private const string GitHubTokenEnvironmentVariable = "GITHUB_TOKEN";

    public static AppConfiguration LoadFromEnvironment(IReadOnlyDictionary<string, string?>? environment = null)
    {
        environment ??= Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .ToDictionary(
                entry => (string)entry.Key,
                entry => entry.Value?.ToString(),
                StringComparer.OrdinalIgnoreCase);

        if (!environment.TryGetValue(GitHubTokenEnvironmentVariable, out var token) ||
            string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("必須先設定系統環境變數 GITHUB_TOKEN。");
        }

        return new AppConfiguration(
            new Uri("https://models.github.ai/inference"),
            "openai/gpt-4.1",
            token);
    }
}
