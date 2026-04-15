using Agent_Test.Models;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.InProc;

namespace Agent_Test.Workflow;

public static class ReviewWorkflowRunner
{
    public static async Task<FinalReviewReport> RunAsync(Microsoft.Agents.AI.Workflows.Workflow workflow, ReviewRequest request, CancellationToken cancellationToken)
    {
        await using var run = await InProcessExecution.RunAsync(workflow, request, cancellationToken: cancellationToken);

        foreach (var workflowEvent in run.OutgoingEvents)
        {
            if (workflowEvent is WorkflowOutputEvent output && output.Is<FinalReviewReport>(out var report))
            {
                return report;
            }
        }

        throw new InvalidOperationException("The workflow completed without yielding a FinalReviewReport.");
    }
}
