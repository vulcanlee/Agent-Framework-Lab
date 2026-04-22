namespace NvidiaTravelAgent.Services;

public interface INvidiaChatClient
{
    Task<T> CompleteJsonAsync<T>(IReadOnlyList<LlmMessage> messages, CancellationToken cancellationToken = default);
}
