using AgentFunctionCall.Services;
using Microsoft.Extensions.AI;

namespace AgentFunctionCall.Tests;

public class InteractionLoggingFormatterTests
{
    [Fact]
    public void FormatMessages_PrefixesEachOutgoingLine()
    {
        var messages = new[]
        {
            new ChatMessage(ChatRole.System, "你是一個四則運算助理。"),
            new ChatMessage(ChatRole.User, "請幫我算\n(2*3 加 10)/8")
        };

        var formatted = InteractionLogFormatter.FormatMessages(messages, ">>>");

        Assert.Contains(">>> [system]", formatted);
        Assert.Contains(">>> 你是一個四則運算助理。", formatted);
        Assert.Contains(">>> [user]", formatted);
        Assert.Contains(">>> 請幫我算", formatted);
        Assert.Contains(">>> (2*3 加 10)/8", formatted);
    }

    [Fact]
    public void FormatMessages_PrefixesEachIncomingLine()
    {
        var messages = new[]
        {
            new ChatMessage(ChatRole.Assistant, "我將先呼叫 multiply。")
        };

        var formatted = InteractionLogFormatter.FormatMessages(messages, "<<<");

        Assert.Contains("<<< [assistant]", formatted);
        Assert.Contains("<<< 我將先呼叫 multiply。", formatted);
    }

    [Fact]
    public void FormatToolCallRequest_UsesIncomingPrefix()
    {
        var formatted = InteractionLogFormatter.FormatToolCallRequest("multiply", "a=2, b=3");

        Assert.Contains("<<< TOOL CALL REQUEST: multiply", formatted);
        Assert.Contains("<<< ARGUMENTS: a=2, b=3", formatted);
    }

    [Fact]
    public void FormatToolResult_UsesIncomingPrefix()
    {
        var formatted = InteractionLogFormatter.FormatToolResult("6");

        Assert.Contains("<<< TOOL RESULT: 6", formatted);
    }

    [Fact]
    public void FormatInferenceReason_UsesReasonPrefixAndDetailedText()
    {
        var formatted = InteractionLogFormatter.FormatInferenceReason(
            "multiply(a=2, b=3)",
            "6",
            "原始算式仍包含後續加法與除法",
            "請 LLM 規劃下一個工具呼叫");

        Assert.Contains("??? 已完成工具呼叫：multiply(a=2, b=3)", formatted);
        Assert.Contains("??? 目前已知結果：6", formatted);
        Assert.Contains("??? 為何需要再次推論：原始算式仍包含後續加法與除法", formatted);
        Assert.Contains("??? 下一輪目標：請 LLM 規劃下一個工具呼叫", formatted);
    }

    [Fact]
    public void FormatUsage_OutputsInputOutputAndTotalTokens()
    {
        var usage = new UsageDetails
        {
            InputTokenCount = 12,
            OutputTokenCount = 7,
            TotalTokenCount = 19
        };

        var formatted = InteractionLogFormatter.FormatUsage(usage);

        Assert.Contains("Input tokens: 12", formatted);
        Assert.Contains("Output tokens: 7", formatted);
        Assert.Contains("Total tokens: 19", formatted);
    }

    [Fact]
    public void FormatUsage_OutputsFallbackMessageWhenUsageMissing()
    {
        var formatted = InteractionLogFormatter.FormatUsage(null);

        Assert.Equal("無法取得 token usage。", formatted);
    }

    [Fact]
    public void PrefixMultiline_PrefixesEveryLine()
    {
        var formatted = InteractionLogFormatter.PrefixMultiline(">>>", "第一行\n第二行");

        Assert.Equal($">>> 第一行{Environment.NewLine}>>> 第二行", formatted);
    }

    [Fact]
    public void MaskSensitiveValues_HidesGithubToken()
    {
        var formatted = InteractionLogFormatter.MaskSensitiveValues("Bearer secret-token-value");

        Assert.DoesNotContain("secret-token-value", formatted);
        Assert.Contains("[REDACTED]", formatted);
    }
}
