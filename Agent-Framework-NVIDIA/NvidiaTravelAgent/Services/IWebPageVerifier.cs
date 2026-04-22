using NvidiaTravelAgent.Models;

namespace NvidiaTravelAgent.Services;

public interface IWebPageVerifier
{
    Task<VerifiedSource> VerifyAsync(string url, CancellationToken cancellationToken = default);
    Task<VerifiedSource> VerifyHtmlAsync(string url, string html, CancellationToken cancellationToken = default);
}
