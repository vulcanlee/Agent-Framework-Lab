using Microsoft.Extensions.AI;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AgentFunctionCall.Services;

public static class InteractionLogFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string FormatMessages(IEnumerable<ChatMessage> messages, string prefix)
    {
        var builder = new StringBuilder();

        foreach (var message in messages)
        {
            builder.AppendLine(PrefixMultiline(prefix, $"[{message.Role.Value}]"));

            if (!string.IsNullOrWhiteSpace(message.Text))
            {
                builder.AppendLine(PrefixMultiline(prefix, MaskSensitiveValues(message.Text)));
            }

            foreach (var content in message.Contents)
            {
                switch (content)
                {
                    case FunctionCallContent functionCall:
                        builder.AppendLine(PrefixMultiline(prefix, $"FunctionCall: {functionCall.Name}"));
                        builder.AppendLine(PrefixMultiline(prefix, $"Arguments: {MaskSensitiveValues(Serialize(functionCall.Arguments))}"));
                        break;
                    case FunctionResultContent functionResult:
                        builder.AppendLine(PrefixMultiline(prefix, $"FunctionResult: {functionResult.CallId}"));
                        builder.AppendLine(PrefixMultiline(prefix, $"Result: {MaskSensitiveValues(Serialize(functionResult.Result))}"));
                        break;
                    case TextContent textContent when textContent.Text != message.Text:
                        builder.AppendLine(PrefixMultiline(prefix, MaskSensitiveValues(textContent.Text)));
                        break;
                }
            }
        }

        return builder.ToString().TrimEnd();
    }

    public static string FormatTaggedBlock(string prefix, string label, string content)
        => PrefixMultiline(prefix, $"{label}: {MaskSensitiveValues(content)}");

    public static string FormatToolCallRequest(string toolName, string arguments)
        => string.Join(
            Environment.NewLine,
            PrefixMultiline("<<<", $"TOOL CALL REQUEST: {toolName}"),
            PrefixMultiline("<<<", $"ARGUMENTS: {MaskSensitiveValues(arguments)}"));

    public static string FormatToolResult(string result)
        => PrefixMultiline("<<<", $"TOOL RESULT: {MaskSensitiveValues(result)}");

    public static string FormatToolError(string error)
        => PrefixMultiline("<<<", $"TOOL ERROR: {MaskSensitiveValues(error)}");

    public static string FormatInferenceReason(string toolCall, string result, string whyNextInference, string nextGoal)
        => string.Join(
            Environment.NewLine,
            PrefixMultiline("???", $"已完成工具呼叫：{toolCall}"),
            PrefixMultiline("???", $"目前已知結果：{result}"),
            PrefixMultiline("???", $"為何需要再次推論：{whyNextInference}"),
            PrefixMultiline("???", $"下一輪目標：{nextGoal}"));

    public static string FormatUsage(UsageDetails? usage)
    {
        if (usage is null)
        {
            return "無法取得 token usage。";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Input tokens: {usage.InputTokenCount}");
        builder.AppendLine($"Output tokens: {usage.OutputTokenCount}");
        builder.AppendLine($"Total tokens: {usage.TotalTokenCount}");

        if (usage.CachedInputTokenCount is long cached)
        {
            builder.AppendLine($"Cached input tokens: {cached}");
        }

        if (usage.ReasoningTokenCount is long reasoning)
        {
            builder.AppendLine($"Reasoning tokens: {reasoning}");
        }

        return builder.ToString().TrimEnd();
    }

    public static string PrefixMultiline(string prefix, string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return prefix;
        }

        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = normalized.Split('\n');
        return string.Join(Environment.NewLine, lines.Select(line => $"{prefix} {line}"));
    }

    public static string MaskSensitiveValues(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return Regex.Replace(
            value,
            "Bearer\\s+\\S+",
            "Bearer [REDACTED]",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    public static string Serialize(object? value)
    {
        if (value is null)
        {
            return "(null)";
        }

        return value is string text
            ? text
            : JsonSerializer.Serialize(value, JsonOptions);
    }
}
