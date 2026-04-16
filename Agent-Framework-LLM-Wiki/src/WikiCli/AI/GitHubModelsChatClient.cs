using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace WikiCli.AI;

public sealed class GitHubModelsChatClient(HttpClient httpClient, string token, string defaultModel) : IChatClient
{
    private const string Endpoint = "https://models.github.ai/inference/chat/completions";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public object? GetService(Type serviceType, object? serviceKey)
    {
        if (serviceType == typeof(HttpClient))
        {
            return httpClient;
        }

        return null;
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Missing GitHub Models token.");
        }

        var requestMessages = new List<GitHubChatMessage>();
        if (!string.IsNullOrWhiteSpace(options?.Instructions))
        {
            requestMessages.Add(new GitHubChatMessage("system", options.Instructions));
        }

        requestMessages.AddRange(messages.Select(MapMessage));

        var request = new GitHubChatRequest(
            options?.ModelId ?? defaultModel,
            requestMessages,
            options?.Temperature,
            options?.MaxOutputTokens);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        httpRequest.Headers.Add("X-GitHub-Api-Version", "2026-03-10");
        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(request, SerializerOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"GitHub Models request failed with status {(int)response.StatusCode}: {payload}");
        }

        var completion = JsonSerializer.Deserialize<GitHubChatCompletionResponse>(payload, SerializerOptions)
            ?? throw new InvalidOperationException("GitHub Models response was empty.");

        var text = completion.Choices
            .Select(choice => choice.Message?.Content)
            .FirstOrDefault(content => !string.IsNullOrWhiteSpace(content))
            ?? throw new InvalidOperationException("GitHub Models response did not contain assistant content.");

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text))
        {
            ModelId = completion.Model ?? request.Model,
            ResponseId = completion.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            RawRepresentation = payload,
        };
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Streaming is intentionally omitted from this teaching sample.");

    public void Dispose()
    {
    }

    private static GitHubChatMessage MapMessage(ChatMessage message)
    {
        var role = message.Role == ChatRole.System
            ? "system"
            : message.Role == ChatRole.Assistant
                ? "assistant"
                : "user";

        return new GitHubChatMessage(role, message.Text ?? string.Empty);
    }

    private sealed record GitHubChatRequest(
        string Model,
        IReadOnlyList<GitHubChatMessage> Messages,
        float? Temperature,
        int? MaxTokens)
    {
        public string Model { get; } = Model;

        public IReadOnlyList<GitHubChatMessage> Messages { get; } = Messages;

        public float? Temperature { get; } = Temperature;

        [JsonPropertyName("max_tokens")]
        public int? MaxTokens { get; } = MaxTokens;
    }

    private sealed record GitHubChatMessage(string Role, string Content);

    private sealed record GitHubChatCompletionResponse(
        string? Id,
        string? Model,
        IReadOnlyList<GitHubChatChoice> Choices);

    private sealed record GitHubChatChoice(GitHubChatAssistantMessage? Message);

    private sealed record GitHubChatAssistantMessage(string? Content);
}
