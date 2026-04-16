using WikiCli.AI;
using WikiCli.Services;

var cancellationToken = CancellationToken.None;
var configuration = AppConfiguration.FromEnvironment(Environment.CurrentDirectory);
var fileService = new WikiFileService(configuration);
var searchService = new WikiSearchService(fileService);
var extractor = new SourceTextExtractor();

IWikiAgent CreateAgent()
{
    var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");

    if (string.IsNullOrWhiteSpace(token))
    {
        throw new InvalidOperationException(
            "Missing GITHUB_TOKEN. Set the environment variable before running ingest, ask, or lint.");
    }

    var httpClient = new HttpClient();
    var chatClient = new GitHubModelsChatClient(httpClient, token, configuration.DefaultModel);
    return new WikiAgent(chatClient, configuration.DefaultModel);
}

var application = new WikiApplication(
    configuration,
    fileService,
    extractor,
    searchService,
    CreateAgent);

return await CliHost.RunAsync(
    args,
    application,
    Console.In,
    Console.Out,
    Console.Error,
    cancellationToken);

public static class CliHost
{
    public static async Task<int> RunAsync(
        string[] args,
        WikiApplication application,
        TextReader input,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        try
        {
            if (args.Length == 0)
            {
                await RunInteractiveAsync(application, input, output, error, cancellationToken);
                return 0;
            }

            var result = await ExecuteCommandAsync(application, args, cancellationToken);
            await output.WriteLineAsync(result);
            return 0;
        }
        catch (CliException ex)
        {
            await error.WriteLineAsync(ex.Message);
            return 1;
        }
        catch (Exception ex)
        {
            await error.WriteLineAsync(ex.Message);
            return 1;
        }
    }

    private static async Task RunInteractiveAsync(
        WikiApplication application,
        TextReader input,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        await output.WriteLineAsync("Wiki CLI interactive mode");
        await output.WriteLineAsync("Type 'help' to see commands. Type 'exit' or 'quit' to leave.");

        while (!cancellationToken.IsCancellationRequested)
        {
            await output.WriteAsync("wiki> ");

            var line = await input.ReadLineAsync();
            if (line is null)
            {
                await output.WriteLineAsync();
                return;
            }

            var commandLine = line.Trim();
            if (commandLine.Length == 0)
            {
                continue;
            }

            try
            {
                var parsed = ParseInteractiveCommand(commandLine);
                if (parsed is null)
                {
                    return;
                }

                var result = await ExecuteCommandAsync(application, parsed, cancellationToken);
                await output.WriteLineAsync(result);
            }
            catch (CliException ex)
            {
                await error.WriteLineAsync(ex.Message);
            }
            catch (Exception ex)
            {
                await error.WriteLineAsync(ex.Message);
            }
        }
    }

    private static async Task<string> ExecuteCommandAsync(
        WikiApplication application,
        IReadOnlyList<string> args,
        CancellationToken cancellationToken)
    {
        var command = args[0].ToLowerInvariant();

        switch (command)
        {
            case "init":
                return await application.InitAsync(cancellationToken);

            case "ingest":
                if (args.Count < 2 || string.IsNullOrWhiteSpace(args[1]))
                {
                    throw new CliException("Usage: wiki ingest <file-or-folder>");
                }

                return await application.IngestAsync(args[1], cancellationToken);

            case "ask":
                if (args.Count < 2 || string.IsNullOrWhiteSpace(args[1]))
                {
                    throw new CliException("Usage: wiki ask \"<question>\"");
                }

                return await application.AskAsync(args[1], cancellationToken);

            case "lint":
                return await application.LintAsync(cancellationToken);

            case "--help":
            case "-h":
            case "help":
                return CommandHelp.Text;

            default:
                throw new CliException($"Unknown command '{args[0]}'.\n\n{CommandHelp.Text}");
        }
    }

    private static string[]? ParseInteractiveCommand(string commandLine)
    {
        var firstWhitespace = commandLine.IndexOfAny([' ', '\t']);
        var command = firstWhitespace >= 0
            ? commandLine[..firstWhitespace]
            : commandLine;
        var remainder = firstWhitespace >= 0
            ? commandLine[(firstWhitespace + 1)..].TrimStart()
            : string.Empty;

        switch (command.ToLowerInvariant())
        {
            case "exit":
            case "quit":
                return null;

            case "ask":
                return string.IsNullOrEmpty(remainder)
                    ? [command]
                    : [command, remainder];

            case "ingest":
                return string.IsNullOrEmpty(remainder)
                    ? [command]
                    : [command, Unquote(remainder)];

            default:
                return string.IsNullOrEmpty(remainder)
                    ? [command]
                    : [command, remainder];
        }
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') ||
             (value[0] == '\'' && value[^1] == '\'')))
        {
            return value[1..^1];
        }

        return value;
    }
}

public sealed class CliException(string message) : Exception(message);

internal static class CommandHelp
{
    public const string Text = """
        Usage:
        - Run without arguments to enter interactive mode.
        - Or run a single command directly:
          wiki init
          wiki ingest <file-or-folder>
          wiki ask "<question>"
          wiki lint
          wiki help

        Interactive mode commands:
        - init
        - ingest <file-or-folder>
        - ask <question>
        - lint
        - help
        - exit / quit

        Environment variables:
        - GITHUB_TOKEN: required for ingest/ask/lint
        - GITHUB_MODEL: optional, defaults to openai/gpt-4.1
        - WIKI_ROOT: optional, defaults to the current working directory
        """;
}
