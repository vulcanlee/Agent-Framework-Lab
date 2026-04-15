using AgentFunctionCall.Services;

namespace AgentFunctionCall.Tests;

public class CalculatorToolsTests
{
    [Fact]
    public void Add_ReturnsSum_AndLogsInvocation()
    {
        var logger = new RecordingInteractionLogger();
        var tools = new CalculatorTools(logger);

        var result = tools.Add(23m, 58m);

        Assert.Equal(81m, result);
        Assert.Contains(logger.Entries, entry => entry.Title == "TOOL CALL" && entry.Content.Contains("<<< TOOL CALL REQUEST: add"));
        Assert.Contains(logger.Entries, entry => entry.Title == "TOOL RESULT" && entry.Content.Contains("<<< TOOL RESULT: 81"));
        Assert.Contains(logger.Entries, entry => entry.Title == "INFERENCE REASON" && entry.Content.Contains("??? 已完成工具呼叫：add(a=23, b=58)"));
    }

    [Fact]
    public void Subtract_ReturnsDifference()
    {
        var tools = new CalculatorTools();

        var result = tools.Subtract(90m, 17m);

        Assert.Equal(73m, result);
    }

    [Fact]
    public void Multiply_ReturnsProduct()
    {
        var tools = new CalculatorTools();

        var result = tools.Multiply(12m, 12m);

        Assert.Equal(144m, result);
    }

    [Fact]
    public void Divide_ReturnsDecimalQuotient()
    {
        var tools = new CalculatorTools();

        var result = tools.Divide(15m, 4m);

        Assert.Equal(3.75m, result);
    }

    [Fact]
    public void Divide_ByZero_ThrowsReadableException_AndLogsError()
    {
        var logger = new RecordingInteractionLogger();
        var tools = new CalculatorTools(logger);

        var exception = Assert.Throws<InvalidOperationException>(() => tools.Divide(10m, 0m));

        Assert.Equal("不可除以 0。", exception.Message);
        Assert.Contains(logger.Entries, entry => entry.Title == "TOOL ERROR" && entry.Content.Contains("<<< TOOL ERROR: 不可除以 0。"));
    }
}
