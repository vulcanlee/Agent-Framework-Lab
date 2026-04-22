using FluentAssertions;
using NvidiaTravelAgent.Configuration;

namespace NvidiaTravelAgent.Tests.Configuration;

public class AppOptionsTests
{
    [Fact]
    public void FromEnvironment_prefers_github_token_when_present()
    {
        var values = new Dictionary<string, string?>
        {
            ["GITHUB_TOKEN"] = "github-key",
            ["Navidia_Vulcan"] = "nvidia-key",
            ["NVIDIA_API_KEY"] = "fallback-key"
        };

        var options = AppOptions.FromEnvironment(name => values.TryGetValue(name, out var value) ? value : null);

        options.NvidiaApiKey.Should().Be("github-key");
    }

    [Fact]
    public void FromEnvironment_uses_navidia_vulcan_when_github_token_is_missing()
    {
        var values = new Dictionary<string, string?>
        {
            ["Navidia_Vulcan"] = "nvidia-key",
            ["NVIDIA_API_KEY"] = "fallback-key"
        };

        var options = AppOptions.FromEnvironment(name => values.TryGetValue(name, out var value) ? value : null);

        options.NvidiaApiKey.Should().Be("nvidia-key");
    }

    [Fact]
    public void FromEnvironment_throws_when_no_api_key_exists()
    {
        var action = () => AppOptions.FromEnvironment(_ => null);

        action.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*GITHUB_TOKEN*");
    }
}
