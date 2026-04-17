namespace AiTopicPulse.Cli.Configuration;

public static class AppOptionsLoader
{
    public static AppOptions Load(Func<string, string?>? getEnvironmentVariable = null)
    {
        getEnvironmentVariable ??= Environment.GetEnvironmentVariable;

        string apiKey = getEnvironmentVariable("OpenAI_Key")
            ?? throw new InvalidOperationException("Environment variable 'OpenAI_Key' is required.");

        return new AppOptions(
            OpenAIApiKey: apiKey,
            Model: "gpt-5-mini",
            Topic: "AI",
            WindowHours: 24);
    }
}
