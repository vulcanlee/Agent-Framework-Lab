namespace NvidiaTravelAgent.Configuration;

public sealed class AppOptions
{
    public string NvidiaApiKey { get; init; } = string.Empty;
    //public string Model { get; init; } = "meta/llama-4-maverick-17b-128e-instruct";
    //public Uri BaseUri { get; init; } = new("https://integrate.api.nvidia.com/v1/");
    public string Model { get; init; } = "openai/gpt-4.1";
    public Uri BaseUri { get; init; } = new("https://models.github.ai/inference/");

    public static AppOptions FromEnvironment(Func<string, string?> getEnvironmentVariable)
    {
        //var primary = getEnvironmentVariable("Navidia_Vulcan");
        var primary = getEnvironmentVariable("GITHUB_TOKEN");
        var fallback = getEnvironmentVariable("NVIDIA_API_KEY");
        var apiKey = !string.IsNullOrWhiteSpace(primary) ? primary : fallback;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("找不到 NVIDIA API Key。請設定環境變數 Navidia_Vulcan，或改用 NVIDIA_API_KEY。");
        }

        return new AppOptions { NvidiaApiKey = apiKey };
    }
}
