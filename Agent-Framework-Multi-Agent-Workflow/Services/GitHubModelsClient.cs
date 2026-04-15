using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Agent_Test.Models;

namespace Agent_Test.Services;

public sealed class GitHubModelsClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly HttpClient _httpClient;
    private readonly ReviewWorkflowOptions _options;

    public GitHubModelsClient(ReviewWorkflowOptions options)
    {
        _options = options;
        _httpClient = new HttpClient();
    }

    public async Task<T> GetStructuredResponseAsync<T>(string systemPrompt, string userPrompt, IReadOnlyList<ImageAttachment>? images, CancellationToken cancellationToken)
    {
        var schema = JsonSchemaFactory.CreateFor<T>();
        var content = await SendChatCompletionAsync(systemPrompt, userPrompt, images, schema, cancellationToken);

        var result = JsonSerializer.Deserialize<T>(content, JsonOptions);
        return result ?? throw new InvalidOperationException($"Model response could not be deserialized to {typeof(T).Name}.");
    }

    public Task<string> GetMarkdownAsync(string systemPrompt, string userPrompt, IReadOnlyList<ImageAttachment>? images, CancellationToken cancellationToken) =>
        SendChatCompletionAsync(systemPrompt, userPrompt, images, null, cancellationToken);

    private async Task<string> SendChatCompletionAsync(string systemPrompt, string userPrompt, IReadOnlyList<ImageAttachment>? images, JsonElement? responseFormat, CancellationToken cancellationToken)
    {
        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Environment variable GITHUB_TOKEN is missing. Set it before running this sample.");
        }

        var hasImages = images is { Count: > 0 };

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.GitHubModelsEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("User-Agent", "agent-test-review-workflow");

        var payload = new Dictionary<string, object?>
        {
            ["model"] = _options.ResolveModelId(hasImages),
            ["temperature"] = 0.1,
            ["max_tokens"] = 1500,
            ["messages"] = new object[]
            {
                new Dictionary<string, string>
                {
                    ["role"] = "system",
                    ["content"] = systemPrompt
                },
                new Dictionary<string, object?>
                {
                    ["role"] = "user",
                    ["content"] = BuildUserContent(userPrompt, images)
                }
            }
        };

        if (responseFormat.HasValue)
        {
            payload["response_format"] = responseFormat.Value;
        }

        request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"GitHub Models request failed with {(int)response.StatusCode}: {responseText}");
        }

        using var document = JsonDocument.Parse(responseText);
        var content = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("GitHub Models returned an empty response.");
        }

        return content;
    }

    public void Dispose() => _httpClient.Dispose();

    private static object[] BuildUserContent(string userPrompt, IReadOnlyList<ImageAttachment>? images)
    {
        var content = new List<object>
        {
            new Dictionary<string, string>
            {
                ["type"] = "text",
                ["text"] = userPrompt
            }
        };

        if (images is { Count: > 0 })
        {
            foreach (var image in images)
            {
                content.Add(new Dictionary<string, object>
                {
                    ["type"] = "image_url",
                    ["image_url"] = new Dictionary<string, string>
                    {
                        ["url"] = image.DataUrl
                    }
                });
            }
        }

        return content.ToArray();
    }

    private static class JsonSchemaFactory
    {
        public static JsonElement CreateFor<T>()
        {
            var schema = typeof(T) == typeof(FeatureParseResult)
                ? """
                  {
                    "type": "object",
                    "additionalProperties": false,
                    "properties": {
                      "partSummary": { "type": "string" },
                      "datums": { "type": "array", "items": { "type": "string" } },
                      "features": { "type": "array", "items": { "type": "string" } },
                      "potentialConcerns": { "type": "array", "items": { "type": "string" } },
                      "imageObservations": { "type": "array", "items": { "type": "string" } }
                    },
                    "required": ["partSummary", "datums", "features", "potentialConcerns", "imageObservations"]
                  }
                  """
                : typeof(T) == typeof(DimensionCheckResult)
                ? """
                  {
                    "type": "object",
                    "additionalProperties": false,
                    "properties": {
                      "summary": { "type": "string" },
                      "issues": {
                        "type": "array",
                        "items": {
                          "type": "object",
                          "additionalProperties": false,
                          "properties": {
                            "category": { "type": "string" },
                            "severity": { "type": "string" },
                            "description": { "type": "string" }
                          },
                          "required": ["category", "severity", "description"]
                        }
                      },
                      "missingDimensions": { "type": "array", "items": { "type": "string" } },
                      "contradictions": { "type": "array", "items": { "type": "string" } }
                    },
                    "required": ["summary", "issues", "missingDimensions", "contradictions"]
                  }
                  """
                : typeof(T) == typeof(ToleranceReviewResult)
                ? """
                  {
                    "type": "object",
                    "additionalProperties": false,
                    "properties": {
                      "summary": { "type": "string" },
                      "issues": {
                        "type": "array",
                        "items": {
                          "type": "object",
                          "additionalProperties": false,
                          "properties": {
                            "category": { "type": "string" },
                            "severity": { "type": "string" },
                            "description": { "type": "string" }
                          },
                          "required": ["category", "severity", "description"]
                        }
                      },
                      "datumGaps": { "type": "array", "items": { "type": "string" } },
                      "overAnnotationRisks": { "type": "array", "items": { "type": "string" } }
                    },
                    "required": ["summary", "issues", "datumGaps", "overAnnotationRisks"]
                  }
                  """
                : throw new NotSupportedException($"No schema factory is registered for {typeof(T).Name}.");

            return JsonDocument.Parse(
                $$"""
                {
                  "type": "json_schema",
                  "json_schema": {
                    "name": "{{typeof(T).Name}}",
                    "schema": {{schema}}
                  }
                }
                """).RootElement.Clone();
        }
    }
}
