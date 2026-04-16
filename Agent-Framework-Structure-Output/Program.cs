using Agent_Framework_Structure_Output.Cli;
using Agent_Framework_Structure_Output.Models;
using Agent_Framework_Structure_Output.Services;

var parser = new CliParser();

if (args.Length == 0)
{
    var repl = new ReplSession(new TranscriptLoader(), new MeetingActionPlanAgent());
    return await repl.RunAsync(CancellationToken.None);
}

CliOptions options;

try
{
    options = parser.Parse(args);
}
catch (CliUsageException ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine();
    Console.Error.WriteLine(CliHelp.Text);
    return 1;
}

if (options.ShowHelp)
{
    Console.WriteLine(CliHelp.Text);
    return 0;
}

try
{
    var transcriptLoader = new TranscriptLoader();
    var transcript = await transcriptLoader.LoadAsync(options, CancellationToken.None);

    var agentService = new MeetingActionPlanAgent();
    MeetingActionPlan plan = await agentService.GenerateAsync(transcript, options.Model, CancellationToken.None);

    Console.WriteLine(options.JsonOutput
        ? OutputRenderer.RenderJson(plan)
        : OutputRenderer.RenderSummary(plan));

    return 0;
}
catch (CliUsageException ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine();
    Console.Error.WriteLine(CliHelp.Text);
    return 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"執行失敗：{ex.Message}");
    return 1;
}
