using AgentFunctionCall.Configuration;
using Microsoft.Agents.AI;

namespace AgentFunctionCall.Services;

public sealed class ConsoleCalculatorApplication
{
    private readonly GitHubModelsAgentFactory _agentFactory;
    private readonly IInteractionLogger _logger;

    public ConsoleCalculatorApplication(GitHubModelsAgentFactory agentFactory, IInteractionLogger logger)
    {
        _agentFactory = agentFactory;
        _logger = logger;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        AppConfiguration configuration;

        try
        {
            configuration = AppConfiguration.LoadFromEnvironment();
        }
        catch (InvalidOperationException exception)
        {
            Console.WriteLine(exception.Message);
            return 1;
        }

        var agent = _agentFactory.Create(configuration);
        AgentSession? session = null;

        Console.WriteLine("四則運算 Agent 已啟動。");
        Console.WriteLine("例如：23 加 58、120 除以 16。輸入 exit 或 quit 可離開。");

        while (!cancellationToken.IsCancellationRequested)
        {
            Console.Write("> ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("quit", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            _logger.LogSection("USER INPUT", InteractionLogFormatter.FormatTaggedBlock(">>>", "USER INPUT", input));

            try
            {
                var response = await agent.RunAsync(input, session, cancellationToken: cancellationToken);
                var output = ExtractText(response);
                _logger.LogSection("FINAL OUTPUT", InteractionLogFormatter.FormatTaggedBlock("<<<", "FINAL RESPONSE", output));
                Console.WriteLine(output);
            }
            catch (Exception exception)
            {
                var message = InteractionLogFormatter.MaskSensitiveValues(exception.Message);
                _logger.LogSection("ERROR", InteractionLogFormatter.FormatTaggedBlock("<<<", "ERROR", message));
                Console.WriteLine($"模型呼叫失敗：{message}");
            }
        }

        return 0;
    }

    private static string ExtractText(AgentResponse response)
    {
        return response.Messages.LastOrDefault()?.Text ?? "目前沒有可顯示的回應。";
    }
}
