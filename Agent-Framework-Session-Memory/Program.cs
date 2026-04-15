using Microsoft.Agents.AI;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

const string defaultModel = "openai/gpt-4.1";
const string endpoint = "https://models.github.ai/inference";
const string summaryPrompt = """
請用繁體中文整理這個 session 到目前為止談過的重點。

請依照下列格式輸出：
1. 主題摘要
2. 使用者提供過的重要資訊
3. 尚未解決或可繼續追問的事項

如果某一項沒有內容，請明確寫出「目前沒有」。
""";
const string instructions = """
你是一個示範多回合 session 記憶的助理。
請自然延續同一段對話，必要時引用先前回合中已提過的資訊。
當使用者要求你整理、回顧、總結之前聊過的內容時，請根據目前 session 中的上下文回答。
請使用繁體中文，回答保持清楚、精簡、友善。
""";

string token = GetRequiredEnvironmentVariable("GITHUB_TOKEN");
string model = Environment.GetEnvironmentVariable("GITHUB_MODEL") ?? defaultModel;

OpenAIClient client = new(
    new ApiKeyCredential(token),
    new OpenAIClientOptions
    {
        Endpoint = new Uri(endpoint)
    });

ChatClient chatClient = client.GetChatClient(model);
AIAgent agent = chatClient.AsAIAgent(instructions: instructions, name: "SessionMemoryAgent");
AgentSession session = await agent.CreateSessionAsync();

PrintBanner(model);

while (true)
{
    Console.Write("\n你> ");
    string? input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input))
    {
        continue;
    }

    string command = input.Trim();

    if (command.Equals("/exit", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("系統> 已結束。");
        break;
    }

    if (command.Equals("/reset", StringComparison.OrdinalIgnoreCase))
    {
        session = await agent.CreateSessionAsync();
        Console.WriteLine("系統> 已建立新的 session，先前對話記憶已清空。");
        continue;
    }

    if (command.Equals("/summary", StringComparison.OrdinalIgnoreCase))
    {
        await RunTurnAsync(agent, session, summaryPrompt);
        continue;
    }

    await RunTurnAsync(agent, session, input);
}

static async Task RunTurnAsync(AIAgent agent, AgentSession session, string prompt)
{
    try
    {
        var response = await agent.RunAsync(prompt, session);
        Console.WriteLine($"\nAgent> {response}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n系統> 呼叫模型時發生錯誤：{ex.Message}");
    }
}

static string GetRequiredEnvironmentVariable(string name)
{
    string? value =
        Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process) ??
        Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User) ??
        Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine);

    if (!string.IsNullOrWhiteSpace(value))
    {
        return value;
    }

    throw new InvalidOperationException(
        $$"""
        找不到必要的環境變數 {{name}}。

        PowerShell 設定範例：
        $env:{{name}} = "your-token"

        設定完成後，請重新執行程式。
        """);
}

static void PrintBanner(string model)
{
    Console.InputEncoding = System.Text.Encoding.UTF8;
    Console.OutputEncoding = System.Text.Encoding.UTF8;

    Console.WriteLine("Microsoft Agent Framework + GitHub Models");
    Console.WriteLine("多回合對話 Session 記憶示範");
    Console.WriteLine($"模型: {model}");
    Console.WriteLine($"端點: {endpoint}");
    Console.WriteLine();
    Console.WriteLine("指令:");
    Console.WriteLine("  /summary  整理目前 session 的對話重點");
    Console.WriteLine("  /reset    建立新的 session，清空目前記憶");
    Console.WriteLine("  /exit     結束程式");
}
