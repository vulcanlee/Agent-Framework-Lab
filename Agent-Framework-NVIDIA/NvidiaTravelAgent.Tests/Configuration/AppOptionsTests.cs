using FluentAssertions;
using NvidiaTravelAgent.Configuration;

namespace NvidiaTravelAgent.Tests.Configuration;

public class AppOptionsTests
{
    [Fact]
    public void FromEnvironment_prefers_Navidia_Vulcan()
    {
        var values = new Dictionary<string, string?>
        {
            ["Navidia_Vulcan"] = "primary-key",
            ["NVIDIA_API_KEY"] = "fallback-key",
        };

        var options = AppOptions.FromEnvironment(name => values.TryGetValue(name, out var value) ? value : null);

        options.NvidiaApiKey.Should().Be("primary-key");
    }

    [Fact]
    public void FromEnvironment_uses_fallback_when_primary_is_missing()
    {
        var values = new Dictionary<string, string?>
        {
            ["NVIDIA_API_KEY"] = "fallback-key",
        };

        var options = AppOptions.FromEnvironment(name => values.TryGetValue(name, out var value) ? value : null);

        options.NvidiaApiKey.Should().Be("fallback-key");
    }

    [Fact]
    public void FromEnvironment_throws_when_no_api_key_exists()
    {
        var action = () => AppOptions.FromEnvironment(_ => null);

        action.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*Navidia_Vulcan*");
    }
}
