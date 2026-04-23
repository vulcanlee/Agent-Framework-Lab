using AgentFrameworkPersistenceMemory.Agent;
using AgentFrameworkPersistenceMemory.Configuration;
using AgentFrameworkPersistenceMemory.Infrastructure;
using AgentFrameworkPersistenceMemory.Memory;
using AgentFrameworkPersistenceMemory.Recall;
using AgentFrameworkPersistenceMemory.Sessions;
using AgentFrameworkPersistenceMemory.Status;
using AgentFrameworkPersistenceMemory.Workflow;
using Microsoft.Extensions.Configuration;

namespace AgentFrameworkPersistenceMemory;

public static class AppBootstrapper
{
    public static PmConsoleApp Create(TextReader input, TextWriter output)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var options = configuration.Get<AppOptions>() ?? new AppOptions();
        var tokenName = options.GitHubModels.TokenEnvironmentVariable;
        var apiKey = Environment.GetEnvironmentVariable(tokenName);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException($"找不到 GitHub Models API Key，請先設定環境變數 {tokenName}。");
        }

        var memoryStore = new PersistentMemoryStore(Path.GetFullPath(options.Memory.FilePath, AppContext.BaseDirectory));
        var modelsClient = new GitHubModelsClient(new HttpClient(), options.GitHubModels, apiKey);
        var sessionManager = new SessionManager();
        var reporter = new AgentStatusReporter(output);
        var workflow = new PmWorkflow(
            memoryStore,
            sessionManager,
            new MemoryRecallService(new GitHubModelsMemoryRelevanceEvaluator(modelsClient)),
            new AgentFrameworkPmAgentService(modelsClient),
            reporter,
            EngineerRoster.Default);

        return new PmConsoleApp(memoryStore, sessionManager, workflow, output, input);
    }
}
