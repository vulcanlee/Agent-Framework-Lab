using Microsoft.Extensions.AI;

var cancellationTokenSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationTokenSource.Cancel();
};

try
{
    var options = AppOptions.Parse(args);

    if (options.ShouldUseInteractiveMode)
    {
        options = PromptForMissingValues(options);
    }

    options.Validate();

    var analyzer = new GitHubModelAnalyzer();
    var result = await analyzer.AnalyzeAsync(options, cancellationTokenSource.Token);

    Console.WriteLine();
    Console.WriteLine("=== Analysis Result ===");
    Console.WriteLine(result.Trim());
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("The operation was cancelled.");
    Environment.ExitCode = 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    Environment.ExitCode = 1;
}

static AppOptions PromptForMissingValues(AppOptions options)
{
    var prompt = options.Prompt;

    if (string.IsNullOrWhiteSpace(prompt))
    {
        Console.Write("Prompt: ");
        prompt = Console.ReadLine();
    }

    return options with
    {
        Prompt = prompt?.Trim()
    };
}
