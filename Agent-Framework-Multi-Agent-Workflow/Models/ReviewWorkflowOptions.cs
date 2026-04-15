namespace Agent_Test.Models;

public sealed record ReviewWorkflowOptions(
    string TextModelId,
    string VisionModelId,
    string GitHubModelsEndpoint,
    string OutputPath)
{
    public static ReviewWorkflowOptions Create(string outputPath) =>
        new(
            TextModelId: "openai/gpt-4.1-mini",
            VisionModelId: "openai/gpt-4.1",
            GitHubModelsEndpoint: "https://models.github.ai/inference/chat/completions",
            OutputPath: Path.GetFullPath(outputPath));

    public string ResolveModelId(bool hasImages)
    {
        if (hasImages)
        {
            if (string.IsNullOrWhiteSpace(VisionModelId))
            {
                throw new InvalidOperationException("A vision-capable model is required when sample-input contains image files.");
            }

            return VisionModelId;
        }

        return TextModelId;
    }
}
