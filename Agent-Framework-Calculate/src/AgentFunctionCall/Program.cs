using AgentFunctionCall.Services;

ConsoleInteractionLogger.ConfigureConsoleEncoding();

var logger = new ConsoleInteractionLogger();
var calculatorTools = new CalculatorTools(logger);
var agentFactory = new GitHubModelsAgentFactory(calculatorTools, logger);
var application = new ConsoleCalculatorApplication(agentFactory, logger);

return await application.RunAsync();
