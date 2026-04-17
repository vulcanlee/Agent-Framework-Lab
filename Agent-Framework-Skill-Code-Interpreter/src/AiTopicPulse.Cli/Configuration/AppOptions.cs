namespace AiTopicPulse.Cli.Configuration;

public sealed record AppOptions(
    string OpenAIApiKey,
    string Model,
    string Topic,
    int WindowHours);
