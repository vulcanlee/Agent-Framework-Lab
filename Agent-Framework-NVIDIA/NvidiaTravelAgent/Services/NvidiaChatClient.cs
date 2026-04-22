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
            ?? throw new InvalidOperationException("NVIDIA 回應為空。");

        var content = payload["choices"]?[0]?["message"]?["content"]?.GetValue<string>()
            ?? throw new InvalidOperationException("NVIDIA 回應不包含 choices[0].message.content。");

        return JsonSerializer.Deserialize<T>(ExtractJson(content), TravelRequestSerializer.Options)
            ?? throw new InvalidOperationException("無法將 NVIDIA 回應解析為目標 JSON。");
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
