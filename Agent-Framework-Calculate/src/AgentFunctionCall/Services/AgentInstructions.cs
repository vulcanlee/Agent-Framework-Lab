using System.Text;

namespace AgentFunctionCall.Services;

public static class AgentInstructions
{
    private const string DefaultCalculator = """
        你是一個四則運算助理。
        你只能處理加減乘除相關的請求。
        遇到任何需要計算的情況，必須呼叫提供的工具，不可以自己心算或直接猜答案。
        如果使用者的問題不是四則運算，請禮貌告知目前只支援加減乘除。
        如果工具回傳錯誤，請直接把錯誤原因用自然語言轉告使用者。
        請使用繁體中文，簡潔回覆。
        """;

    public static string Calculator => LoadCalculator();

    public static string LoadCalculator(string? promptPath = null)
    {
        promptPath ??= Path.Combine(AppContext.BaseDirectory, "Prompts", "agent-instructions.md");

        if (!File.Exists(promptPath))
        {
            return DefaultCalculator;
        }

        return File.ReadAllText(promptPath, Encoding.UTF8).Trim();
    }
}
