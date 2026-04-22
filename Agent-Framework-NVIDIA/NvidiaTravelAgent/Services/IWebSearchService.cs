using NvidiaTravelAgent.Models;

namespace NvidiaTravelAgent.Services;

public interface IWebSearchService
{
    Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default);
}
