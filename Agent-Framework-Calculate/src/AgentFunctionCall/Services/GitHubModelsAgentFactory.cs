using AgentFunctionCall.Configuration;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace AgentFunctionCall.Services;

public sealed class GitHubModelsAgentFactory
{
    private readonly CalculatorTools _calculatorTools;
    private readonly IInteractionLogger _logger;

    public GitHubModelsAgentFactory(CalculatorTools calculatorTools, IInteractionLogger logger)
    {
        _calculatorTools = calculatorTools;
        _logger = logger;
    }

    public ChatClientAgent Create(AppConfiguration configuration)
    {
        var client = new OpenAIClient(
            new ApiKeyCredential(configuration.GitHubToken),
            new OpenAIClientOptions
            {
                Endpoint = configuration.Endpoint
            });

        ChatClient chatClient = client.GetChatClient(configuration.Model);

        return chatClient.AsAIAgent(
            AgentInstructions.Calculator,
            "CalculatorAgent",
            "使用 GitHub Models 與本地工具處理自然語言四則運算。",
            CreateTools(),
            innerClient => new ChatClientBuilder(innerClient)
                .UseFunctionInvocation()
                .Use((messages, options, inner, cancellationToken) => LogAndInvokeAsync(messages, options, inner, cancellationToken), null)
                .Build(),
            null,
            null);
    }

    private async Task<ChatResponse> LogAndInvokeAsync(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
        ChatOptions? options,
        IChatClient inner,
        CancellationToken cancellationToken)
    {
        var messageList = messages.ToList();
        LogOutgoingMessages(messageList);

        var response = await inner.GetResponseAsync(messageList, options, cancellationToken);

        LogIncomingMessages(response);
        _logger.LogSection("TOKEN USAGE", InteractionLogFormatter.FormatTaggedBlock("<<<", "TOKEN USAGE", InteractionLogFormatter.FormatUsage(response.Usage)));

        return response;
    }

    private void LogOutgoingMessages(IReadOnlyList<Microsoft.Extensions.AI.ChatMessage> messages)
    {
        _logger.LogSection("SYSTEM PROMPT", InteractionLogFormatter.FormatTaggedBlock(">>>", "SYSTEM PROMPT", AgentInstructions.Calculator));

        var hasFunctionResult = messages.Any(message => message.Contents.OfType<FunctionResultContent>().Any());
        var label = hasFunctionResult ? "FOLLOW-UP PAYLOAD" : "USER MESSAGE";
        _logger.LogSection(label, InteractionLogFormatter.FormatTaggedBlock(">>>", label, InteractionLogFormatter.FormatMessages(messages, ">>>")));
    }

    private void LogIncomingMessages(ChatResponse response)
    {
        foreach (var message in response.Messages)
        {
            var functionCalls = message.Contents.OfType<FunctionCallContent>().ToList();

            foreach (var functionCall in functionCalls)
            {
                _logger.LogSection(
                    "TOOL CALL REQUEST",
                    InteractionLogFormatter.FormatToolCallRequest(
                        functionCall.Name,
                        InteractionLogFormatter.Serialize(functionCall.Arguments)));
            }

            if (!string.IsNullOrWhiteSpace(message.Text))
            {
                var label = functionCalls.Count == 0 ? "FINAL RESPONSE" : "LLM RESPONSE";
                _logger.LogSection(label, InteractionLogFormatter.FormatTaggedBlock("<<<", label, message.Text));
            }
            else if (functionCalls.Count == 0)
            {
                _logger.LogSection("LLM RESPONSE", InteractionLogFormatter.FormatMessages([message], "<<<"));
            }
        }
    }

    private IList<AITool> CreateTools()
    {
        return
        [
            AIFunctionFactory.Create(_calculatorTools.Add, name: "add", description: "將兩個數字相加並回傳結果。"),
            AIFunctionFactory.Create(_calculatorTools.Subtract, name: "subtract", description: "用第一個數字減去第二個數字並回傳結果。"),
            AIFunctionFactory.Create(_calculatorTools.Multiply, name: "multiply", description: "將兩個數字相乘並回傳結果。"),
            AIFunctionFactory.Create(_calculatorTools.Divide, name: "divide", description: "用第一個數字除以第二個數字並回傳結果。")
        ];
    }
}
