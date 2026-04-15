using AgentFunctionCall.Configuration;

namespace AgentFunctionCall.Tests;

public class AppConfigurationTests
{
    [Fact]
    public void LoadFromEnvironment_Throws_WhenGithubTokenMissing()
    {
        var environment = new Dictionary<string, string?>();

        var exception = Assert.Throws<InvalidOperationException>(() => AppConfiguration.LoadFromEnvironment(environment));

        Assert.Equal("必須先設定系統環境變數 GITHUB_TOKEN。", exception.Message);
    }

    [Fact]
    public void LoadFromEnvironment_ReturnsExpectedSettings()
    {
        var environment = new Dictionary<string, string?>
        {
            ["GITHUB_TOKEN"] = "test-token"
        };

        var configuration = AppConfiguration.LoadFromEnvironment(environment);

        Assert.Equal("test-token", configuration.GitHubToken);
        Assert.Equal("https://models.github.ai/inference", configuration.Endpoint.AbsoluteUri.TrimEnd('/'));
        Assert.Equal("openai/gpt-4.1", configuration.Model);
    }
}
