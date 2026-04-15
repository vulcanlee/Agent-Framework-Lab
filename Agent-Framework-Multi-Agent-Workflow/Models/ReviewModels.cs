using System.Text.Json.Serialization;

namespace Agent_Test.Models;

public sealed record ReviewRequest(
    [property: JsonPropertyName("partName")] string PartName,
    [property: JsonPropertyName("featureJson")] string FeatureJson,
    [property: JsonPropertyName("dimensionData")] string DimensionData,
    [property: JsonPropertyName("drawingNotes")] string DrawingNotes,
    IReadOnlyList<ImageAttachment> ImageAttachments);

public sealed record ImageAttachment(
    string FileName,
    string MimeType,
    string DataUrl);

public sealed record FeatureParseResult(
    [property: JsonPropertyName("partSummary")] string PartSummary,
    [property: JsonPropertyName("datums")] IReadOnlyList<string> Datums,
    [property: JsonPropertyName("features")] IReadOnlyList<string> Features,
    [property: JsonPropertyName("potentialConcerns")] IReadOnlyList<string> PotentialConcerns,
    [property: JsonPropertyName("imageObservations")] IReadOnlyList<string> ImageObservations);

public sealed record FeatureReviewContext(
    ReviewRequest ReviewRequest,
    FeatureParseResult FeatureParseResult);

public sealed record DimensionIssue(
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("description")] string Description);

public sealed record DimensionCheckResult(
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("issues")] IReadOnlyList<DimensionIssue> Issues,
    [property: JsonPropertyName("missingDimensions")] IReadOnlyList<string> MissingDimensions,
    [property: JsonPropertyName("contradictions")] IReadOnlyList<string> Contradictions);

public sealed record DimensionReviewContext(
    ReviewRequest ReviewRequest,
    FeatureParseResult FeatureParseResult,
    DimensionCheckResult DimensionCheckResult);

public sealed record ToleranceIssue(
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("description")] string Description);

public sealed record ToleranceReviewResult(
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("issues")] IReadOnlyList<ToleranceIssue> Issues,
    [property: JsonPropertyName("datumGaps")] IReadOnlyList<string> DatumGaps,
    [property: JsonPropertyName("overAnnotationRisks")] IReadOnlyList<string> OverAnnotationRisks);

public sealed record ToleranceReviewContext(
    ReviewRequest ReviewRequest,
    FeatureParseResult FeatureParseResult,
    DimensionCheckResult DimensionCheckResult,
    ToleranceReviewResult ToleranceReviewResult);

public sealed record FinalReviewReport(
    [property: JsonPropertyName("markdown")] string Markdown);

public sealed record ReviewWorkflowResult(FinalReviewReport Report, string OutputPath);
