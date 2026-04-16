using System.ClientModel;
using Agent_Framework_Structure_Output.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Agent_Framework_Structure_Output.Services;

internal sealed class MeetingActionPlanAgent
{
    private static readonly Uri GitHubModelsEndpoint = new("https://models.github.ai/inference/");

    private const string Instructions =
        """
        你是一個專門協助專案團隊整理會議紀錄的 AI 代理程式。
        你的任務是將輸入的會議內容整理成結構化資料，協助團隊快速掌握摘要、決策、待辦、風險與待確認事項。

        請遵守以下規則：
        1. 只能根據輸入內容整理資訊，不要自行捏造人名、日期、承諾或結論。
        2. 如果負責人、截止日或優先級沒有被明確提及，可以填 null 或使用「待確認」這類保守值，但不要猜測。
        3. 摘要要簡潔，重點放在決策、工作安排與阻塞風險。
        4. 行動項目要具體可執行。
        5. 如果會議內容存在不確定性，請整理成 follow_up_questions。
        6. 請以繁體中文輸出內容。
        """;

    public async Task<MeetingActionPlan> GenerateAsync(string transcript, string model, CancellationToken cancellationToken)
    {
        string token = Environment.GetEnvironmentVariable("GITHUB_TOKEN")
            ?? throw new InvalidOperationException("找不到環境變數 `GITHUB_TOKEN`，請先設定 GitHub Models API Token。");

        if (string.IsNullOrWhiteSpace(transcript))
        {
            throw new Cli.CliUsageException("輸入內容為空，請提供會議逐字稿、摘要或透過 sample 模式執行。");
        }

        IChatClient chatClient = new OpenAI.Chat.ChatClient(
                model: model,
                credential: new ApiKeyCredential(token),
                options: new OpenAIClientOptions
                {
                    Endpoint = GitHubModelsEndpoint
                })
            .AsIChatClient();

        AIAgent agent = chatClient.AsAIAgent(new ChatClientAgentOptions
        {
            Name = "MeetingActionPlanner",
            Description = "將會議內容整理成結構化行動清單。",
            ChatOptions = new ChatOptions
            {
                ModelId = model,
                Instructions = Instructions
            }
        });

        AgentResponse<MeetingActionPlan> response = await agent.RunAsync<MeetingActionPlan>(
            $"""
             請整理以下會議內容，輸出結構化的會議行動清單。

             會議內容：
             {transcript}
             """,
            cancellationToken: cancellationToken);

        return response.Result;
    }
}
