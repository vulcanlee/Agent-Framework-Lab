using Agent_Test.Models;
using Agent_Test.Services;
using Agent_Test.Workflow;
using System.Text;

var cancellationToken = CancellationToken.None;
Console.OutputEncoding = Encoding.UTF8;

try
{
    var cliOptions = CliOptions.Parse(args);
    var app = new ReviewWorkflowApplication();
    var result = await app.RunAsync(cliOptions, cancellationToken);

    Console.WriteLine();
    Console.WriteLine("[Final Result] 所有 agents 協作完成的最終報告");
    Console.WriteLine(new string('=', 48));
    Console.WriteLine(result.Report.Markdown);
    Console.WriteLine();
    Console.WriteLine($"Report written to: {result.OutputPath}");
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}
