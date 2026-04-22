using NvidiaTravelAgent.Configuration;
using NvidiaTravelAgent.Models;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NvidiaTravelAgent.Services;

public sealed class NvidiaChatClient : INvidiaChatClient
{
    private readonly HttpClient _httpClient;
    private readonly AppOptions _options;

    public NvidiaChatClient(HttpClient httpClient, AppOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<T> CompleteJsonAsync<T>(IReadOnlyList<LlmMessage> messages, CancellationToken cancellationToken = default)
    {
        var workingMessages = messages.ToList();
        string? lastContent = null;
        Exception? lastException = null;

        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                lastContent = await SendChatCompletionAsync(workingMessages, cancellationToken);
                var json = ExtractJson(lastContent);
                var root = JsonNode.Parse(json) as JsonObject
                    ?? throw new InvalidOperationException("模型回傳內容不是有效的 JSON 物件。");

                var normalized = ModelJsonNormalizer.NormalizeFor<T>(root);
                ModelJsonNormalizer.ValidateRequiredFields<T>(normalized);

                return JsonSerializer.Deserialize<T>(normalized.ToJsonString(), TravelRequestSerializer.Options)
                    ?? throw new InvalidOperationException("模型回傳 JSON 無法轉換為目標型別。");
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                lastException = ex;

                if (attempt == 0)
                {
                    workingMessages = BuildRepairMessages<T>(messages, lastContent, ex);
                    continue;
                }
            }
        }

        throw new ModelOutputException(GetFailureMessage<T>(), lastException);
    }

    private async Task<string> SendChatCompletionAsync(IReadOnlyList<LlmMessage> messages, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_options.BaseUri, "chat/completions"));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.NvidiaApiKey);
        request.Headers.Accept.ParseAdd("application/json");
        request.Content = JsonContent.Create(new
        {
            model = _options.Model,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
            max_tokens = 1024,
            temperature = 0.2,
            top_p = 1.0,
            frequency_penalty = 0.0,
            presence_penalty = 0.0,
            stream = false
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("模型服務沒有回傳內容。");

        return payload["choices"]?[0]?["message"]?["content"]?.GetValue<string>()
            ?? throw new InvalidOperationException("模型回應缺少 choices[0].message.content。");
    }

    private static List<LlmMessage> BuildRepairMessages<T>(IReadOnlyList<LlmMessage> originalMessages, string? lastContent, Exception ex)
    {
        return
        [
            .. originalMessages,
            new LlmMessage("assistant", lastContent ?? string.Empty),
            new LlmMessage("user", $"""
                你上一則回覆的 JSON 格式不符合要求，請重新輸出完全合法的 JSON。
                錯誤原因：{ex.Message}
                正確 schema：
                {GetSchemaDescription<T>()}
                請只輸出 JSON，陣列欄位必須真的輸出為 JSON array。
                """)
        ];
    }

    private static string GetSchemaDescription<T>()
    {
        if (typeof(T).Name == "TravelRequest")
        {
            return """
                {
                  "destination": "string",
                  "days": 3,
                  "travelStyle": "string",
                  "transportationPreference": "string",
                  "budget": "string",
                  "specialRequirements": ["string", "string"]
                }
                """;
        }

        if (typeof(T).Name == "TravelPlan")
        {
            return """
                {
                  "summary": "string",
                  "dailyPlans": [
                    {
                      "day": 1,
                      "theme": "string",
                      "items": [
                        {
                          "category": "string",
                          "name": "string",
                          "description": "string"
                        }
                      ]
                    }
                  ],
                  "transportationNotes": ["string"],
                  "accommodationNotes": ["string"],
                  "cautions": ["string"]
                }
                """;
        }

        return "請輸出符合目標型別的 JSON。";
    }

    private static string GetFailureMessage<T>()
    {
        if (typeof(T).Name == "TravelRequest")
        {
            return "需求解析失敗，請稍微簡化描述或重試。";
        }

        if (typeof(T).Name == "TravelPlan")
        {
            return "行程生成失敗，模型回傳格式不符合預期，請稍後再試。";
        }

        return "模型輸出格式不符合預期。";
    }

    private static string ExtractJson(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstBrace = trimmed.IndexOf('{');
            var lastBrace = trimmed.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                return trimmed[firstBrace..(lastBrace + 1)];
            }
        }

        var builder = new StringBuilder(trimmed.Length);
        var capture = false;
        var depth = 0;
        foreach (var character in trimmed)
        {
            if (character == '{')
            {
                capture = true;
                depth++;
            }

            if (capture)
            {
                builder.Append(character);
            }

            if (character == '}')
            {
                depth--;
                if (capture && depth == 0)
                {
                    return builder.ToString();
                }
            }
        }

        return trimmed;
    }
}
