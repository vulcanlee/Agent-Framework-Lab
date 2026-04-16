using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using WikiCli.Services;

namespace WikiCli.AI;

public sealed class WikiAgent : IWikiAgent
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ChatClientAgent _agent;

    public WikiAgent(IChatClient chatClient, string modelId)
    {
        _agent = new ChatClientAgent(
            chatClient,
            new ChatClientAgentOptions
            {
                Name = "WikiAgent",
                Description = "Maintains a local markdown wiki from raw sources.",
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    ModelId = modelId,
                    Temperature = 0.2f,
                    MaxOutputTokens = 4000,
                },
            },
            NullLoggerFactory.Instance,
            services: null);
    }

    public async Task<IngestAgentResult> GenerateIngestPlanAsync(
        IngestRequest request,
        CancellationToken cancellationToken)
    {
        var prompt = PromptTemplates.BuildIngestPrompt(request);
        var response = await _agent.RunAsync<IngestAgentResult>(
            prompt,
            null,
            JsonOptions,
            null,
            cancellationToken);

        return response.Result ?? throw new InvalidOperationException("The ingest response was empty.");
    }

    public async Task<string> AnswerQuestionAsync(AskRequest request, CancellationToken cancellationToken)
    {
        var response = await _agent.RunAsync(
            PromptTemplates.BuildAskPrompt(request),
            null,
            null,
            cancellationToken);

        return response.Text ?? throw new InvalidOperationException("The answer response was empty.");
    }

    public async Task<string> CreateLintReportAsync(LintRequest request, CancellationToken cancellationToken)
    {
        var response = await _agent.RunAsync(
            PromptTemplates.BuildLintPrompt(request),
            null,
            null,
            cancellationToken);

        return response.Text ?? throw new InvalidOperationException("The lint response was empty.");
    }

    private static class PromptTemplates
    {
        public static string BuildIngestPrompt(IngestRequest request)
        {
            var builder = new StringBuilder();
            builder.AppendLine(
                """
                You are maintaining a persistent markdown wiki that sits between raw sources and user questions.
                Return valid JSON only. Do not wrap the JSON in Markdown fences.

                JSON schema:
                {
                  "sourceSummary": "one sentence",
                  "sourcePageMarkdown": "full markdown page",
                  "additionalPages": [
                    {
                      "category": "topics|entities|analyses",
                      "title": "page title",
                      "summary": "one sentence",
                      "markdown": "full markdown page"
                    }
                  ],
                  "logSummary": "short log entry"
                }

                Rules:
                - The source page and every additional page must be in markdown.
                - Use this exact page structure:
                  # Title
                  Summary paragraph
                  ## Key Facts
                  bullet list
                  ## Related Pages
                  bullet list using markdown links when possible
                  ## Sources
                  bullet list
                  Last updated: YYYY-MM-DD
                - Keep additionalPages to at most 3 items.
                - Update existing topics by rewriting the full page content, not by describing edits.
                - Preserve uncertainty when the source is incomplete.
                """);
            builder.AppendLine();
            builder.AppendLine($"Source file: {request.SourceFileName}");
            builder.AppendLine($"Source path: {request.SourceRelativePath}");
            builder.AppendLine();
            builder.AppendLine("Current index:");
            builder.AppendLine(request.CurrentIndex);
            builder.AppendLine();
            builder.AppendLine("Potentially related wiki pages:");
            builder.AppendLine(FormatPages(request.RelatedPages));
            builder.AppendLine();
            builder.AppendLine("Source text:");
            builder.AppendLine(TrimLongText(request.SourceText, 12000));

            return builder.ToString();
        }

        public static string BuildAskPrompt(AskRequest request)
        {
            var builder = new StringBuilder();
            builder.AppendLine(
                """
                Answer the question using only the wiki content provided below.
                If the wiki does not contain enough evidence, say so plainly.
                Cite claims with markdown citations like [sources/meeting-notes.md] or [topics/product-roadmap.md].
                Do not use the raw source collection directly.
                Keep the answer concise and practical.
                """);
            builder.AppendLine();
            builder.AppendLine($"Question: {request.Question}");
            builder.AppendLine();
            builder.AppendLine("Wiki index:");
            builder.AppendLine(request.IndexMarkdown);
            builder.AppendLine();
            builder.AppendLine("Candidate pages:");
            builder.AppendLine(FormatPages(request.CandidatePages, includeContent: true));
            return builder.ToString();
        }

        public static string BuildLintPrompt(LintRequest request)
        {
            var builder = new StringBuilder();
            builder.AppendLine(
                """
                Turn the heuristic lint signals into a clean markdown report for the wiki maintainers.
                Use this structure:
                # Wiki Lint Report
                Summary paragraph
                ## Findings
                bullet list
                ## Suggested Next Actions
                bullet list
                Last updated: YYYY-MM-DD
                Keep the report grounded in the provided data only.
                """);
            builder.AppendLine();
            builder.AppendLine("Current index:");
            builder.AppendLine(request.IndexMarkdown);
            builder.AppendLine();
            builder.AppendLine("Heuristic signals:");
            builder.AppendLine(request.HeuristicReport);
            builder.AppendLine();
            builder.AppendLine("Candidate pages:");
            builder.AppendLine(FormatPages(request.CandidatePages, includeContent: false));
            return builder.ToString();
        }

        private static string FormatPages(IReadOnlyList<SearchResult> pages, bool includeContent = false)
        {
            if (pages.Count == 0)
            {
                return "- None";
            }

            var builder = new StringBuilder();

            foreach (var page in pages)
            {
                builder.AppendLine($"- {page.RelativePath}");
                builder.AppendLine($"  Title: {page.Title}");
                builder.AppendLine($"  Summary: {page.Summary}");

                if (includeContent)
                {
                    builder.AppendLine("  Content:");
                    builder.AppendLine(Indent(TrimLongText(page.Content, 4000), "    "));
                }
            }

            return builder.ToString().TrimEnd();
        }

        private static string Indent(string text, string prefix)
        {
            return string.Join(
                Environment.NewLine,
                text.Split(['\r', '\n'], StringSplitOptions.None).Select(line => prefix + line));
        }

        private static string TrimLongText(string value, int maxChars)
        {
            if (value.Length <= maxChars)
            {
                return value;
            }

            return $"{value[..maxChars]}{Environment.NewLine}[...truncated...]";
        }
    }
}
