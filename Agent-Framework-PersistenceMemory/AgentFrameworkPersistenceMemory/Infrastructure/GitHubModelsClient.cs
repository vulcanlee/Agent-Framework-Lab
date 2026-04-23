using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AgentFrameworkPersistenceMemory.Configuration;

namespace AgentFrameworkPersistenceMemory.Infrastructure;

public sealed record ModelUsage(int InputTokens, int OutputTokens, int OtherTokens);

public sealed record ModelCompletionResult(string Content, ModelUsage Usage);

public interface IGitHubModelsClient
{
    Task<ModelCompletionResult> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken);
}

public sealed class GitHubModelsClient(HttpClient httpClient, GitHubModelsOptions options, string apiKey) : IGitHubModelsClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient = httpClient;
    private readonly GitHubModelsOptions _options = options;
    private readonly string _apiKey = apiKey;

    public async Task<ModelCompletionResult> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/chat/completions");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Headers.Add("X-GitHub-Api-Version", _options.ApiVersion);
        request.Content = JsonContent.Create(new
        {
            model = _options.Model,
            temperature = 0.2,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        var completion = JsonSerializer.Deserialize<GitHubModelsChatCompletionResponse>(payload, JsonOptions)
            ?? throw new InvalidOperationException("GitHub Models 回應格式無法解析。");

        var content = completion.Choices.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("GitHub Models 沒有回傳任何內容。");
        }

        var promptTokens = completion.Usage?.PromptTokens ?? 0;
        var completionTokens = completion.Usage?.CompletionTokens ?? 0;
        var totalTokens = completion.Usage?.TotalTokens;
        var otherTokens = Math.Max(0, (totalTokens ?? (promptTokens + completionTokens)) - promptTokens - completionTokens);

        return new ModelCompletionResult(content, new ModelUsage(promptTokens, completionTokens, otherTokens));
    }

    private sealed class GitHubModelsChatCompletionResponse
    {
        public List<Choice> Choices { get; init; } = [];

        public UsageDto? Usage { get; init; }
    }

    private sealed class Choice
    {
        public Message? Message { get; init; }
    }

    private sealed class Message
    {
        public string? Content { get; init; }
    }

    private sealed class UsageDto
    {
        public int? PromptTokens { get; init; }

        public int? CompletionTokens { get; init; }

        public int? TotalTokens { get; init; }
    }
}
