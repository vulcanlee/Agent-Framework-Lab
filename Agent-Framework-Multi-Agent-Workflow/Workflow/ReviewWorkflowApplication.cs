using Agent_Test.Models;

namespace Agent_Test.Workflow;

public sealed class ReviewWorkflowApplication
{
    public async Task<ReviewWorkflowResult> RunAsync(CliOptions cliOptions, CancellationToken cancellationToken)
    {
        EnsureGitHubToken();

        var request = await Services.ReviewRequestLoader.LoadAsync(cliOptions.InputPath, cancellationToken);
        var workflowOptions = ReviewWorkflowOptions.Create(cliOptions.OutputPath);

        using var client = new Services.GitHubModelsClient(workflowOptions);
        var workflow = new ReviewWorkflowFactory(client).Create();
        var report = await ReviewWorkflowRunner.RunAsync(workflow, request, cancellationToken);

        await Services.ReviewReportWriter.WriteAsync(report, workflowOptions.OutputPath, cancellationToken);
        return new ReviewWorkflowResult(report, workflowOptions.OutputPath);
    }

    private static void EnsureGitHubToken()
    {
        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Environment variable GITHUB_TOKEN is missing. Set it before running this sample.");
        }
    }
}
