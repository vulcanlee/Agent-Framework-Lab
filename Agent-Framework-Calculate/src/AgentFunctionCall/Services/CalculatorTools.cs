using System.ComponentModel;

namespace AgentFunctionCall.Services;

public sealed class CalculatorTools
{
    private readonly IInteractionLogger? _logger;

    public CalculatorTools(IInteractionLogger? logger = null)
    {
        _logger = logger;
    }

    [Description("將兩個數字相加。")]
    public decimal Add(decimal a, decimal b)
    {
        return Execute("add", a, b, () => a + b);
    }

    [Description("用第一個數字減去第二個數字。")]
    public decimal Subtract(decimal a, decimal b)
    {
        return Execute("subtract", a, b, () => a - b);
    }

    [Description("將兩個數字相乘。")]
    public decimal Multiply(decimal a, decimal b)
    {
        return Execute("multiply", a, b, () => a * b);
    }

    [Description("用第一個數字除以第二個數字。")]
    public decimal Divide(decimal a, decimal b)
    {
        return Execute("divide", a, b, () =>
        {
            if (b == 0)
            {
                throw new InvalidOperationException("不可除以 0。");
            }

            return a / b;
        });
    }

    private decimal Execute(string toolName, decimal a, decimal b, Func<decimal> action)
    {
        var toolCall = $"{toolName}(a={a}, b={b})";
        _logger?.LogSection("TOOL CALL", InteractionLogFormatter.FormatToolCallRequest(toolName, $"a={a}, b={b}"));

        try
        {
            var result = action();
            _logger?.LogSection("TOOL RESULT", InteractionLogFormatter.FormatToolResult(result.ToString()));
            _logger?.LogSection(
                "INFERENCE REASON",
                InteractionLogFormatter.FormatInferenceReason(
                    toolCall,
                    result.ToString(),
                    "系統需要把最新工具結果帶回 LLM，確認整體問題是否已完成，或是否還需要下一個工具呼叫。",
                    "請 LLM 依據目前上下文決定下一步，可能是繼續呼叫工具或直接產生最終答案。"));
            return result;
        }
        catch (Exception exception)
        {
            _logger?.LogSection("TOOL ERROR", InteractionLogFormatter.FormatToolError(exception.Message));
            throw;
        }
    }
}
