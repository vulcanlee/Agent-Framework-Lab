using AiTopicPulse.Cli.Configuration;

namespace AiTopicPulse.Cli.Tests.Configuration;

public sealed class AppOptionsLoaderTests
{
    [Fact]
    public void Load_reads_OpenAI_Key_and_uses_the_default_model()
    {
        Dictionary<string, string?> environment = new()
        {
            ["OpenAI_Key"] = "test-key"
        };

        AppOptions options = AppOptionsLoader.Load(
            key => environment.TryGetValue(key, out string? value) ? value : null);

        Assert.Equal("test-key", options.OpenAIApiKey);
        Assert.Equal("gpt-5-mini", options.Model);
        Assert.Equal("AI", options.Topic);
        Assert.Equal(24, options.WindowHours);
    }

    [Fact]
    public void Load_throws_when_OpenAI_Key_is_missing()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => AppOptionsLoader.Load(_ => null));

        Assert.Contains("OpenAI_Key", exception.Message, StringComparison.Ordinal);
    }
}
