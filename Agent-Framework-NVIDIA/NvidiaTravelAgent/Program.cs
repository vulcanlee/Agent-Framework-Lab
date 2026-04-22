using NvidiaTravelAgent.Agents;
using NvidiaTravelAgent.Configuration;
using NvidiaTravelAgent.Services;
using System.Text;

var exitCode = await CliApplication.RunAsync(args, new SystemConsole(), CreateAgent, CancellationToken.None);
return exitCode;

static TravelPlannerAgent CreateAgent(AppOptions options)
{
    var httpClient = new HttpClient();
    var llm = new NvidiaChatClient(httpClient, options);
    var search = new WebSearchService(httpClient);
    var verifier = new WebPageVerifier(httpClient);
    var planner = new TravelPlannerEngine(llm, search, verifier, new ItineraryComposer());
    return new TravelPlannerAgent(planner);
}

public interface IAppConsole
{
    Encoding InputEncoding { get; set; }
    Encoding OutputEncoding { get; set; }
    string? ReadLine();
    void Write(string value);
    void WriteLine(string value);
    void WriteErrorLine(string value);
}

internal sealed class SystemConsole : IAppConsole
{
    public Encoding InputEncoding
    {
        get => Console.InputEncoding;
        set => Console.InputEncoding = value;
    }

    public Encoding OutputEncoding
    {
        get => Console.OutputEncoding;
        set => Console.OutputEncoding = value;
    }

    public string? ReadLine() => Console.ReadLine();

    public void Write(string value) => Console.Write(value);

    public void WriteLine(string value) => Console.WriteLine(value);

    public void WriteErrorLine(string value) => Console.Error.WriteLine(value);
}

internal static class CliApplication
{
    public static Task<int> RunAsync(
        string[] args,
        CancellationToken cancellationToken)
        => RunAsync(args, new SystemConsole(), CreateDefaultAgent, cancellationToken);

    internal static async Task<int> RunAsync(
        string[] args,
        IAppConsole console,
        Func<AppOptions, TravelPlannerAgent> agentFactory,
        CancellationToken cancellationToken)
    {
        console.InputEncoding = Encoding.UTF8;
        console.OutputEncoding = Encoding.UTF8;

        if (args.Length == 0)
        {
            return await RunReplAsync(console, agentFactory, cancellationToken);
        }

        var command = args.FirstOrDefault()?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(command) || command is "--help" or "-h" or "help")
        {
            PrintHelp(console);
            return 0;
        }

        var options = AppOptions.FromEnvironment(Environment.GetEnvironmentVariable);
        var agent = agentFactory(options);

        return command switch
        {
            "plan" => await RunPlanAsync(agent, args.Skip(1).ToArray(), console, cancellationToken),
            "repl" => await RunReplAsync(console, _ => agent, cancellationToken),
            _ => UnknownCommand(command, console)
        };
    }

    private static TravelPlannerAgent CreateDefaultAgent(AppOptions options)
    {
        var httpClient = new HttpClient();
        var llm = new NvidiaChatClient(httpClient, options);
        var search = new WebSearchService(httpClient);
        var verifier = new WebPageVerifier(httpClient);
        var planner = new TravelPlannerEngine(llm, search, verifier, new ItineraryComposer());
        return new TravelPlannerAgent(planner);
    }

    private static async Task<int> RunPlanAsync(
        TravelPlannerAgent agent,
        string[] args,
        IAppConsole console,
        CancellationToken cancellationToken)
    {
        var request = ReadRequest(args);
        if (string.IsNullOrWhiteSpace(request))
        {
            console.WriteErrorLine("請使用 --request 提供旅遊需求描述。");
            return 1;
        }

        var response = await agent.RunAsync(request, cancellationToken: cancellationToken);
        console.WriteLine(response.Text);
        return 0;
    }

    private static async Task<int> RunReplAsync(
        IAppConsole console,
        Func<AppOptions, TravelPlannerAgent> agentFactory,
        CancellationToken cancellationToken)
    {
        console.WriteLine("NVIDIA Travel Agent REPL");
        console.WriteLine("輸入需求開始規劃，輸入 reset 清空目前對話，輸入 exit 離開。");

        var options = AppOptions.FromEnvironment(Environment.GetEnvironmentVariable);
        var agent = agentFactory(options);
        var session = await agent.CreateSessionAsync(cancellationToken);

        while (true)
        {
            console.Write("> ");
            var input = console.ReadLine();

            if (input is null)
            {
                console.WriteErrorLine("沒有可互動的標準輸入，REPL 已結束。請從終端機以互動模式執行。");
                return 1;
            }

            if (string.Equals(input, "exit", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (string.Equals(input, "reset", StringComparison.OrdinalIgnoreCase))
            {
                TravelPlannerAgent.ResetSession(session);
                console.WriteLine("已清空目前對話。");
                continue;
            }

            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            var response = await agent.RunAsync(input, session, cancellationToken: cancellationToken);
            console.WriteLine(response.Text);
        }
    }

    private static string? ReadRequest(IReadOnlyList<string> args)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], "--request", StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    private static int UnknownCommand(string command, IAppConsole console)
    {
        console.WriteErrorLine($"未知命令: {command}");
        PrintHelp(console);
        return 1;
    }

    private static void PrintHelp(IAppConsole console)
    {
        console.WriteLine("用法:");
        console.WriteLine("  dotnet run --project .\\NvidiaTravelAgent\\NvidiaTravelAgent.csproj");
        console.WriteLine("  dotnet run --project .\\NvidiaTravelAgent\\NvidiaTravelAgent.csproj -- repl");
        console.WriteLine("  dotnet run --project .\\NvidiaTravelAgent\\NvidiaTravelAgent.csproj -- plan --request \"三天兩夜台南美食散步，不自駕，預算中等\"");
    }
}
