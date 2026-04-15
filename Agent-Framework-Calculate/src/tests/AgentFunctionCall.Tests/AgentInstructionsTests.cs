using AgentFunctionCall.Services;

namespace AgentFunctionCall.Tests;

public class AgentInstructionsTests
{
    [Fact]
    public void LoadCalculator_ReturnsMarkdownContents_WhenFileExists()
    {
        var tempFile = Path.GetTempFileName();

        try
        {
            File.WriteAllText(tempFile, "這是測試用 prompt。", System.Text.Encoding.UTF8);

            var instructions = AgentInstructions.LoadCalculator(tempFile);

            Assert.Equal("這是測試用 prompt。", instructions);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void LoadCalculator_FallsBackToDefault_WhenFileMissing()
    {
        var instructions = AgentInstructions.LoadCalculator("Z:\\missing-agent-instructions.md");

        Assert.Contains("你只能處理加減乘除", instructions);
        Assert.Contains("必須呼叫提供的工具", instructions);
        Assert.Contains("請使用繁體中文", instructions);
    }
}
