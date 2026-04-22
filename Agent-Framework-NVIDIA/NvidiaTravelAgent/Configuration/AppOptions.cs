namespace NvidiaTravelAgent.Configuration;

public sealed class AppOptions
{
    public string NvidiaApiKey { get; init; } = string.Empty;
    public string Model { get; init; } = "openai/gpt-4.1";
    public Uri BaseUri { get; init; } = new("https://models.github.ai/inference/");

    public static AppOptions FromEnvironment(Func<string, string?> getEnvironmentVariable)
    {
        var githubToken = getEnvironmentVariable("GITHUB_TOKEN");
        var nvidiaPrimary = getEnvironmentVariable("Navidia_Vulcan");
        var nvidiaFallback = getEnvironmentVariable("NVIDIA_API_KEY");

        var apiKey = FirstNonEmpty(githubToken, nvidiaPrimary, nvidiaFallback);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "找不到可用的 API Key。請先設定 GITHUB_TOKEN、Navidia_Vulcan 或 NVIDIA_API_KEY。");
        }

        return new AppOptions { NvidiaApiKey = apiKey };
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}
