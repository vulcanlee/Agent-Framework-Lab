using OpenAI;
using OpenAI.Responses;

namespace AiTopicPulse.Cli.Analysis;

using AiTopicPulse.Cli.Configuration;
using AiTopicPulse.Cli.Status;

public sealed class OpenAiCodeInterpreterAnalyzer(AppOptions options) : ITrendAnalyzer
{
    private readonly AppOptions _options = options;

    public async Task<PulseReport> AnalyzeAsync(
        AnalysisDataset dataset,
        IStatusReporter statusReporter,
        CancellationToken cancellationToken)
    {
        OpenAIClient client = new(_options.OpenAIApiKey);
        ResponsesClient responsesClient = client.GetResponsesClient();
        CreateResponseOptions options = CreateResponseOptions(_options, dataset);

        ResponseResult? response = null;
        await foreach (StreamingResponseUpdate update in responsesClient.CreateResponseStreamingAsync(options, cancellationToken))
        {
            string? statusMessage = ResponseUpdateStatusFormatter.TryFormat(update);
            if (!string.IsNullOrWhiteSpace(statusMessage))
            {
                statusReporter.Report($"[Analysis Agent] {statusMessage}");
            }

            if (update is StreamingResponseCompletedUpdate completed)
            {
                response = completed.Response;
            }
        }

        response ??= await responsesClient.CreateResponseAsync(options, cancellationToken);
        string markdown = ExtractMarkdown(response);
        string transcript = ExtractCodeInterpreterTranscript(response);

        return new PulseReport(
            Markdown: markdown,
            SuccessfulSources: dataset.SuccessfulSources,
            FailedSources: dataset.FailedSources,
            CodeInterpreterTranscript: transcript);
    }

    internal static CreateResponseOptions CreateResponseOptions(AppOptions appOptions, AnalysisDataset dataset)
    {
        string prompt = CodeInterpreterPromptFactory.Create(dataset);
        CodeInterpreterToolContainer container = new(
            CodeInterpreterToolContainerConfiguration.CreateAutomaticContainerConfiguration([]));

        return new CreateResponseOptions
        {
            Model = appOptions.Model,
            StreamingEnabled = true,
            Instructions = """
                You are an AI topic analyst. When a JSON dataset is provided, you must use the code interpreter.
                Parse the dataset in Python, deduplicate similar stories, rank them by recency and engagement,
                and return Traditional Chinese markdown only.
                """,
            InputItems = { ResponseItem.CreateUserMessageItem(prompt) },
            Tools = { ResponseTool.CreateCodeInterpreterTool(container) }
        };
    }

    private static string ExtractMarkdown(ResponseResult response)
    {
        List<string> markdownBlocks = [];

        foreach (ResponseItem item in response.OutputItems)
        {
            if (item is MessageResponseItem message && message.Role == MessageRole.Assistant)
            {
                foreach (ResponseContentPart part in message.Content)
                {
                    if (!string.IsNullOrWhiteSpace(part.Text))
                    {
                        markdownBlocks.Add(part.Text);
                    }
                }
            }
        }

        return markdownBlocks.Count > 0
            ? string.Join(Environment.NewLine + Environment.NewLine, markdownBlocks)
            : "The model returned no markdown output.";
    }

    private static string ExtractCodeInterpreterTranscript(ResponseResult response)
    {
        List<string> snippets = [];

        foreach (ResponseItem item in response.OutputItems)
        {
            if (item is not CodeInterpreterCallResponseItem call)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(call.Code))
            {
                snippets.Add($"Code:{Environment.NewLine}{call.Code}");
            }

            foreach (CodeInterpreterCallOutput output in call.Outputs)
            {
                if (output is CodeInterpreterCallLogsOutput logs && !string.IsNullOrWhiteSpace(logs.Logs))
                {
                    snippets.Add($"Logs:{Environment.NewLine}{logs.Logs}");
                }
            }
        }

        return string.Join(Environment.NewLine + Environment.NewLine, snippets);
    }
}
