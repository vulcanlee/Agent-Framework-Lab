using System.ClientModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

internal sealed class GitHubModelAnalyzer
{
    private static readonly Uri GitHubModelsEndpoint = new("https://models.github.ai/inference");

    public async Task<string> AnalyzeAsync(AppOptions options, CancellationToken cancellationToken)
    {
        var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (string.IsNullOrWhiteSpace(githubToken))
        {
            throw new InvalidOperationException("GITHUB_TOKEN is not set. Please configure it before running the demo.");
        }

        var model = options.GetResolvedModel();
        var fileInput = await FileInputBuilder.BuildAsync(options, cancellationToken);

        var openAiClient = new OpenAIClient(
            new ApiKeyCredential(githubToken),
            new OpenAIClientOptions
            {
                Endpoint = GitHubModelsEndpoint
            });

        var chatClient = openAiClient.GetChatClient(model);
        var agent = chatClient.AsAIAgent(
            name: "file-analyzer",
            instructions: BuildInstructions(fileInput),
            description: "Analyzes a user prompt together with one file.");

        var session = await agent.CreateSessionAsync(cancellationToken: cancellationToken);
        var userMessage = new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, fileInput.PromptContents.ToList());
        var response = await agent.RunAsync(userMessage, session, cancellationToken: cancellationToken);

        if (!string.IsNullOrWhiteSpace(response.Text))
        {
            return response.Text;
        }

        throw new InvalidOperationException("The model returned an empty response.");
    }

    private static string BuildInstructions(FileInput fileInput)
    {
        var fileTypeLabel = fileInput.IsImage ? "image" : "UTF-8 text file";

        return $"""
            You are a helpful analysis assistant.
            The user will provide a question that references a local file path, and the application will attach that {fileTypeLabel}.
            Use the file as primary evidence, be specific, and write the answer in Traditional Chinese.
            If the file is an image, comment on visible layout, labeling, spacing, or measurement issues.
            If the file is a text file, cite concrete details from the attached content in your explanation.
            """;
    }
}
