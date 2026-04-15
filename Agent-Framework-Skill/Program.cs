using System.ClientModel;
using System.Text.Json;
using Microsoft.Agents.AI;
using OpenAI;
using OpenAI.Chat;

namespace AgentSkillsDemo;

internal static class Program
{
    private const string DefaultModelId = "openai/gpt-4.1-mini";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static async Task Main(string[] args)
    {
        string token = GetRequiredEnvironmentVariable("GITHUB_TOKEN");
        string modelId = Environment.GetEnvironmentVariable("GITHUB_MODEL") ?? DefaultModelId;
        string skillsDirectory = ResolveSkillsDirectory();

        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("Microsoft Agent Framework + GitHub Models Skill Demo");
        Console.WriteLine($"Model: {modelId}");
        Console.WriteLine($"Skills directory: {skillsDirectory}");
        Console.WriteLine("Commands: /skills, /clear, /exit");
        Console.WriteLine();

        AgentSkillsProvider skillsProvider = new(skillsDirectory, options: new AgentSkillsProviderOptions
        {
            DisableCaching = true,
        });

        ChatClient chatClient = new(
            modelId,
            new ApiKeyCredential(token),
            new OpenAIClientOptions
            {
                Endpoint = new Uri("https://models.github.ai/inference"),
            });

        AIAgent agent = chatClient.AsAIAgent(new ChatClientAgentOptions
        {
            Name = "skill-demo-agent",
            Description = "A small demo agent that can load local skills from disk.",
            ChatOptions = new()
            {
                Temperature = 0.2f,
                Instructions =
                    """
                    You are a helpful assistant for a console demo.
                    Keep answers concise and explicit.
                    If a user's request matches a local skill, load the skill before answering.
                    Requests about article outlines, tutorial structure, blog post plans, release notes, changelogs, or change summaries must load the matching skill first.
                    If no skill is needed, answer directly without pretending that a skill was used.
                    """,
            },
            AIContextProviders = [skillsProvider],
        });

        AgentSession session = await agent.CreateSessionAsync();

        while (true)
        {
            Console.Write("User> ");
            string? input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            if (string.Equals(input, "/exit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (string.Equals(input, "/clear", StringComparison.OrdinalIgnoreCase))
            {
                session = await agent.CreateSessionAsync();
                Console.WriteLine("Session cleared.");
                Console.WriteLine();
                continue;
            }

            if (string.Equals(input, "/skills", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string skill in ListSkillNames(skillsDirectory))
                {
                    Console.WriteLine($"- {skill}");
                }

                Console.WriteLine();
                continue;
            }

            object response = await agent.RunAsync(input, session);
            IReadOnlyList<string> loadedSkills = ExtractLoadedSkills(response);

            Console.WriteLine(
                loadedSkills.Count == 0
                    ? "Loaded skills: (none)"
                    : $"Loaded skills: {string.Join(", ", loadedSkills)}");
            Console.WriteLine($"Assistant> {ExtractAssistantText(response)}");
            Console.WriteLine();
        }
    }

    private static string GetRequiredEnvironmentVariable(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"{name} is not set.");

    private static string ResolveSkillsDirectory()
    {
        string currentDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), "skills");
        if (Directory.Exists(currentDirectoryPath))
        {
            return currentDirectoryPath;
        }

        string appDirectoryPath = Path.Combine(AppContext.BaseDirectory, "skills");
        if (Directory.Exists(appDirectoryPath))
        {
            return appDirectoryPath;
        }

        throw new DirectoryNotFoundException("Unable to find a skills directory.");
    }

    private static IReadOnlyList<string> ListSkillNames(string skillsDirectory) =>
        Directory.GetFiles(skillsDirectory, "SKILL.md", SearchOption.AllDirectories)
            .Select(Path.GetDirectoryName)
            .OfType<string>()
            .Select(Path.GetFileName)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray()!;

    private static string ExtractAssistantText(object response)
    {
        string? text = response.GetType().GetProperty("Text")?.GetValue(response)?.ToString();
        if (!string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        object? message = response.GetType().GetProperty("Message")?.GetValue(response);
        string? messageText = message?.GetType().GetProperty("Text")?.GetValue(message)?.ToString();
        if (!string.IsNullOrWhiteSpace(messageText))
        {
            return messageText;
        }

        return response.ToString() ?? string.Empty;
    }

    private static IReadOnlyList<string> ExtractLoadedSkills(object response)
    {
        HashSet<string> loadedSkills = new(StringComparer.OrdinalIgnoreCase);
        IEnumerable<object> messages = ReadObjects(response, "Messages");

        foreach (object message in messages)
        {
            foreach (object content in ReadObjects(message, "Contents"))
            {
                if (!string.Equals(content.GetType().Name, "FunctionCallContent", StringComparison.Ordinal) ||
                    !string.Equals(ReadString(content, "Name"), "load_skill", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string? skillName = TryReadSkillName(content.GetType().GetProperty("Arguments")?.GetValue(content));
                if (!string.IsNullOrWhiteSpace(skillName))
                {
                    loadedSkills.Add(skillName);
                }
            }
        }

        return loadedSkills.OrderBy(static name => name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string? TryReadSkillName(object? arguments)
    {
        if (arguments is null)
        {
            return null;
        }

        if (arguments is IDictionary<string, object?> dictionary)
        {
            foreach (string key in new[] { "skill_name", "skillName", "name" })
            {
                if (dictionary.TryGetValue(key, out object? value) && value is not null)
                {
                    return value.ToString();
                }
            }
        }

        if (arguments is JsonElement jsonElement)
        {
            return TryReadSkillNameFromJsonElement(jsonElement);
        }

        string raw = arguments.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(raw);
            return TryReadSkillNameFromJsonElement(document.RootElement);
        }
        catch (JsonException)
        {
            return raw;
        }
    }

    private static string? TryReadSkillNameFromJsonElement(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString();
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (string propertyName in new[] { "skill_name", "skillName", "name" })
        {
            if (element.TryGetProperty(propertyName, out JsonElement property))
            {
                return property.ValueKind == JsonValueKind.String
                    ? property.GetString()
                    : property.GetRawText();
            }
        }

        return JsonSerializer.Serialize(element, JsonOptions);
    }

    private static IEnumerable<object> ReadObjects(object target, string propertyName)
    {
        object? value = target.GetType().GetProperty(propertyName)?.GetValue(target);
        return value is IEnumerable<object> typedValues
            ? typedValues
            : value is System.Collections.IEnumerable values
                ? values.Cast<object>()
                : Array.Empty<object>();
    }

    private static string? ReadString(object target, string propertyName) =>
        target.GetType().GetProperty(propertyName)?.GetValue(target)?.ToString();
}
