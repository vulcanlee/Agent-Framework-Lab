using OpenAI.Responses;

namespace AiTopicPulse.Cli.Analysis;

public static class ResponseUpdateStatusFormatter
{
    public static string? TryFormat(object update) =>
        update switch
        {
            StreamingResponseCreatedUpdate => "Analysis request created.",
            StreamingResponseInProgressUpdate => "Model is processing the request.",
            StreamingResponseCodeInterpreterCallInProgressUpdate => "Code Interpreter started.",
            StreamingResponseCodeInterpreterCallInterpretingUpdate => "Code Interpreter is analyzing the dataset.",
            StreamingResponseCodeInterpreterCallCompletedUpdate => "Code Interpreter finished running.",
            StreamingResponseCompletedUpdate => "Model response completed.",
            _ => null
        };
}
