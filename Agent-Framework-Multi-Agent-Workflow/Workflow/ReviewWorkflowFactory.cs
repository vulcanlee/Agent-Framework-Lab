using Agent_Test.Models;
using Agent_Test.Services;
using Microsoft.Agents.AI.Workflows;

namespace Agent_Test.Workflow;

public sealed class ReviewWorkflowFactory
{
    private readonly GitHubModelsClient _client;

    public ReviewWorkflowFactory(GitHubModelsClient client)
    {
        _client = client;
    }

    public Microsoft.Agents.AI.Workflows.Workflow Create()
    {
        var featureParser = ExecutorBindingExtensions.BindAsExecutor<ReviewRequest, FeatureReviewContext>(
            (input, _, cancellationToken) => FeatureParserAsync(input, cancellationToken),
            "feature-parser");

        var dimensionChecker = ExecutorBindingExtensions.BindAsExecutor<FeatureReviewContext, DimensionReviewContext>(
            (input, _, cancellationToken) => DimensionCheckerAsync(input, cancellationToken),
            "dimension-checker");

        var toleranceReviewer = ExecutorBindingExtensions.BindAsExecutor<DimensionReviewContext, ToleranceReviewContext>(
            (input, _, cancellationToken) => ToleranceReviewerAsync(input, cancellationToken),
            "tolerance-reviewer");

        var reportAgent = ExecutorBindingExtensions.BindAsExecutor<ToleranceReviewContext, FinalReviewReport>(
            (input, _, cancellationToken) => ReportAgentAsync(input, cancellationToken),
            "report-agent");

        var builder = new WorkflowBuilder(featureParser)
            .WithName("Drawing Review Workflow")
            .WithDescription("Sequential review flow for feature parsing, dimension checking, tolerance review, and reporting.")
            .AddChain(featureParser, [dimensionChecker, toleranceReviewer, reportAgent], false)
            .WithOutputFrom(reportAgent);

        return builder.Build();
    }

    private async ValueTask<FeatureReviewContext> FeatureParserAsync(ReviewRequest request, CancellationToken cancellationToken)
    {
        WriteAgentStarted("Feature Parser", "解析零件特徵、孔位、槽與基準");

        const string systemPrompt = """
            You are Feature Parser, the first reviewer in a mechanical drawing review workflow.
            Analyze the input and output only valid JSON matching the requested schema.
            Focus on part summary, feature list, datum references, image observations, and likely review concerns.
            If drawing images are attached, use them together with the textual input.
            Keep each bullet-sized string short and concrete.
            """;

        var imageText = request.ImageAttachments.Count == 0
            ? "No drawing images were attached."
            : string.Join(Environment.NewLine, request.ImageAttachments.Select(image => $"- {image.FileName} ({image.MimeType})"));

        var userPrompt = $$"""
            Please parse the following part information.

            Part name:
            {{request.PartName}}

            Feature JSON:
            {{request.FeatureJson}}

            Dimension data:
            {{request.DimensionData}}

            Drawing notes:
            {{request.DrawingNotes}}

            Attached drawing images:
            {{imageText}}
            """;

        var result = await _client.GetStructuredResponseAsync<FeatureParseResult>(systemPrompt, userPrompt, request.ImageAttachments, cancellationToken);

        WriteAgentCompleted(
            "Feature Parser",
            [
                $"零件摘要: {result.PartSummary}",
                $"特徵數量: {result.Features.Count}",
                $"基準數量: {result.Datums.Count}",
                $"圖片觀察: {result.ImageObservations.Count}"
            ]);

        return new FeatureReviewContext(request, result);
    }

    private async ValueTask<DimensionReviewContext> DimensionCheckerAsync(FeatureReviewContext context, CancellationToken cancellationToken)
    {
        WriteAgentStarted("Dimension Checker", "檢查尺寸完整性、重複與矛盾");

        const string systemPrompt = """
            You are Dimension Checker, the second reviewer in a drawing review workflow.
            Review completeness, duplicates, contradictions, and missing dimensions.
            Output only valid JSON matching the requested schema.
            If there is no issue, return empty arrays and explain briefly in the summary.
            """;

        var imageObservations = context.FeatureParseResult.ImageObservations.Count == 0
            ? "- No image-based observations were provided."
            : string.Join(Environment.NewLine, context.FeatureParseResult.ImageObservations.Select(observation => $"- {observation}"));

        var userPrompt = $$"""
            Review the parsed feature result below.

            Part name:
            {{context.ReviewRequest.PartName}}

            Raw dimension data:
            {{context.ReviewRequest.DimensionData}}

            Raw drawing notes:
            {{context.ReviewRequest.DrawingNotes}}

            Part summary:
            {{context.FeatureParseResult.PartSummary}}

            Datums:
            {{string.Join(", ", context.FeatureParseResult.Datums)}}

            Features:
            {{string.Join(Environment.NewLine, context.FeatureParseResult.Features.Select(feature => $"- {feature}"))}}

            Image observations:
            {{imageObservations}}

            Potential concerns:
            {{string.Join(Environment.NewLine, context.FeatureParseResult.PotentialConcerns.Select(concern => $"- {concern}"))}}
            """;

        var result = await _client.GetStructuredResponseAsync<DimensionCheckResult>(systemPrompt, userPrompt, null, cancellationToken);

        WriteAgentCompleted(
            "Dimension Checker",
            [
                $"摘要: {result.Summary}",
                $"問題數量: {result.Issues.Count}",
                $"缺少尺寸: {result.MissingDimensions.Count}",
                $"矛盾項目: {result.Contradictions.Count}"
            ]);

        return new DimensionReviewContext(context.ReviewRequest, context.FeatureParseResult, result);
    }

    private async ValueTask<ToleranceReviewContext> ToleranceReviewerAsync(DimensionReviewContext context, CancellationToken cancellationToken)
    {
        WriteAgentStarted("Tolerance Reviewer", "檢查公差合理性、過度標註與基準缺失");

        const string systemPrompt = """
            You are Tolerance Reviewer, the third reviewer in a drawing review workflow.
            Check whether tolerance notes look reasonable, over-annotated, or missing datum references.
            Output only valid JSON matching the requested schema.
            You may infer likely tolerance risks from the dimension review findings, but keep assumptions explicit.
            """;

        var issuesText = context.DimensionCheckResult.Issues.Count == 0
            ? "- No dimension issues were reported."
            : string.Join(Environment.NewLine, context.DimensionCheckResult.Issues.Select(issue => $"- [{issue.Severity}] {issue.Category}: {issue.Description}"));

        var userPrompt = $$"""
            Review tolerance quality using the dimension review result below.

            Part name:
            {{context.ReviewRequest.PartName}}

            Raw drawing notes:
            {{context.ReviewRequest.DrawingNotes}}

            Parsed datums:
            {{string.Join(", ", context.FeatureParseResult.Datums)}}

            Summary:
            {{context.DimensionCheckResult.Summary}}

            Dimension issues:
            {{issuesText}}

            Missing dimensions:
            {{string.Join(Environment.NewLine, context.DimensionCheckResult.MissingDimensions.Select(item => $"- {item}"))}}

            Contradictions:
            {{string.Join(Environment.NewLine, context.DimensionCheckResult.Contradictions.Select(item => $"- {item}"))}}
            """;

        var result = await _client.GetStructuredResponseAsync<ToleranceReviewResult>(systemPrompt, userPrompt, null, cancellationToken);

        WriteAgentCompleted(
            "Tolerance Reviewer",
            [
                $"摘要: {result.Summary}",
                $"公差問題: {result.Issues.Count}",
                $"基準缺口: {result.DatumGaps.Count}",
                $"過度標註風險: {result.OverAnnotationRisks.Count}"
            ]);

        return new ToleranceReviewContext(context.ReviewRequest, context.FeatureParseResult, context.DimensionCheckResult, result);
    }

    private async ValueTask<FinalReviewReport> ReportAgentAsync(ToleranceReviewContext context, CancellationToken cancellationToken)
    {
        WriteAgentStarted("Report Agent", "彙整所有 agent 的審查結果並產出最終報告");

        const string systemPrompt = """
            You are Report Agent, the final reviewer in a drawing review workflow.
            Write a concise Markdown review report in Traditional Chinese.
            Use exactly these section headings:
            ## 零件摘要
            ## 尺寸問題清單
            ## 公差問題清單
            ## 建議修正
            ## 整體結論
            Do not wrap the Markdown in JSON.
            """;

        var imageObservations = context.FeatureParseResult.ImageObservations.Count == 0
            ? "- 無圖片觀察補充。"
            : string.Join(Environment.NewLine, context.FeatureParseResult.ImageObservations.Select(observation => $"- {observation}"));

        var dimensionIssuesText = context.DimensionCheckResult.Issues.Count == 0
            ? "- 未發現明顯尺寸問題。"
            : string.Join(Environment.NewLine, context.DimensionCheckResult.Issues.Select(issue => $"- [{issue.Severity}] {issue.Category}: {issue.Description}"));

        var issuesText = context.ToleranceReviewResult.Issues.Count == 0
            ? "- 未發現明顯公差問題。"
            : string.Join(Environment.NewLine, context.ToleranceReviewResult.Issues.Select(issue => $"- [{issue.Severity}] {issue.Category}: {issue.Description}"));

        var userPrompt = $$"""
            Please turn the final review context into a Markdown report.

            Part name:
            {{context.ReviewRequest.PartName}}

            Feature summary:
            {{context.FeatureParseResult.PartSummary}}

            Image observations:
            {{imageObservations}}

            Dimension summary:
            {{context.DimensionCheckResult.Summary}}

            Tolerance summary:
            {{context.ToleranceReviewResult.Summary}}

            Dimension issues:
            {{dimensionIssuesText}}

            Tolerance issues:
            {{issuesText}}

            Datum gaps:
            {{string.Join(Environment.NewLine, context.ToleranceReviewResult.DatumGaps.Select(item => $"- {item}"))}}

            Over-annotation risks:
            {{string.Join(Environment.NewLine, context.ToleranceReviewResult.OverAnnotationRisks.Select(item => $"- {item}"))}}
            """;

        var markdown = await _client.GetMarkdownAsync(systemPrompt, userPrompt, null, cancellationToken);
        var result = new FinalReviewReport(markdown);

        var sectionCount = markdown.Split("## ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
        WriteAgentCompleted(
            "Report Agent",
            [
                $"報告章節數: {sectionCount}",
                $"報告長度: {markdown.Length} 字元"
            ]);

        return result;
    }

    private static void WriteAgentStarted(string agentName, string description)
    {
        Console.WriteLine();
        Console.WriteLine($"[Agent Start] {agentName}");
        Console.WriteLine($"- 任務: {description}");
    }

    private static void WriteAgentCompleted(string agentName, IReadOnlyList<string> summaryLines)
    {
        Console.WriteLine($"[Agent Done] {agentName}");
        foreach (var line in summaryLines)
        {
            Console.WriteLine($"- {line}");
        }
    }
}
