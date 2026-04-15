using System.ClientModel;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

namespace AgentMemoryDemo;

internal static class Program
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static async Task Main(string[] args)
    {
        string? token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            Console.WriteLine("Environment variable GITHUB_TOKEN is required.");
            return;
        }

        string model = Environment.GetEnvironmentVariable("GITHUB_MODEL") ?? "openai/gpt-4.1-mini";
        string workspace = Directory.GetCurrentDirectory();
        string sessionPath = Path.Combine(workspace, "session.json");
        string memoryPath = Path.Combine(workspace, "memory-store.json");
        string logPath = Path.Combine(workspace, "conversation-log.jsonl");

        var openAiClient = new OpenAIClient(
            new ApiKeyCredential(token),
            new OpenAIClientOptions
            {
                Endpoint = new Uri("https://models.github.ai/inference"),
            });

        var chatClient = openAiClient.GetChatClient(model);
        var summarizerClient = chatClient.AsIChatClient();

        AIAgent agent = summarizerClient.AsAIAgent(
            instructions: """
                你是一個會在多輪對話中延續上下文的助理。
                回答要自然、精簡、準確。
                如果系統提供了摘要記憶，可以把它當成輔助上下文，但不要把不確定的內容說成既定事實。
                """,
            name: "MemoryDemoAgent");

        AIAgent memoryAgent = summarizerClient.AsAIAgent(
            instructions: """
                你是一個對話記憶整理器。
                請把輸入整理成結構化摘要記憶。
                只保留高價值資訊，不要逐字轉錄完整對話。
                不確定的資訊不要寫入 profile 或 preferences。
                conversation_summary 要精簡、可讀，維持 2 到 5 句。
                important_topics 與 open_loops 只保留真正重要的項目。
                如果沒有新資訊，保留原值。
                """,
            name: "MemorySummarizer");

        AgentSession session = await RestoreSessionAsync(agent, sessionPath);
        MemoryStore memory = await MemoryStoreFile.LoadAsync(memoryPath);
        int turn = await ConversationLogFile.GetNextTurnNumberAsync(logPath);
        long cumulativeTokens = await ConversationLogFile.GetCumulativeTotalTokensAsync(logPath);

        Console.WriteLine("Agent Memory Demo (.NET 10 + Microsoft Agent Framework + GitHub Models)");
        Console.WriteLine($"Model: {model}");
        Console.WriteLine("Commands: :memory, :log, :reset, :exit");
        Console.WriteLine();

        while (true)
        {
            Console.Write("You > ");
            string? input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            if (input.Equals(":exit", StringComparison.OrdinalIgnoreCase))
            {
                await SaveSessionAsync(agent, session, sessionPath);
                Console.WriteLine("Bye.");
                return;
            }

            if (input.Equals(":memory", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(JsonSerializer.Serialize(memory, JsonOptions));
                continue;
            }

            if (input.Equals(":log", StringComparison.OrdinalIgnoreCase))
            {
                string recentLog = await ConversationLogFile.ReadRecentTurnsAsync(logPath, count: 5);
                Console.WriteLine(string.IsNullOrWhiteSpace(recentLog) ? "No conversation log yet." : recentLog);
                continue;
            }

            if (input.Equals(":reset", StringComparison.OrdinalIgnoreCase))
            {
                FileHelpers.DeleteIfExists(sessionPath);
                FileHelpers.DeleteIfExists(memoryPath);
                FileHelpers.DeleteIfExists(logPath);

                session = await agent.CreateSessionAsync();
                memory = MemoryStore.CreateEmpty();
                turn = 1;

                Console.WriteLine("All persisted memory files were reset.");
                continue;
            }

            string enrichedPrompt = MemoryPromptComposer.Compose(memory, input);
            AgentResponse response = await agent.RunAsync(enrichedPrompt, session);
            string assistantText = ResponseTextExtractor.Extract(response);

            Console.WriteLine();
            Console.WriteLine($"Agent > {assistantText}");
            Console.WriteLine();

            ConversationTurnLog sourceTurn = new(
                Timestamp: DateTimeOffset.UtcNow,
                SessionId: SessionIdentity.Get(session),
                Turn: turn,
                UserMessage: input,
                AssistantMessage: assistantText);

            MemoryUpdateResult memoryUpdate = await MemorySummarizer.UpdateAsync(memoryAgent, memory, sourceTurn);

            UsageSnapshot dialogueUsage = UsageSnapshot.From(response.Usage);
            UsageSnapshot memoryUsage = memoryUpdate.Usage;
            TurnTokenUsage turnUsage = TurnTokenUsage.Create(dialogueUsage, memoryUsage, cumulativeTokens);

            var entry = new ConversationTurnLog(
                sourceTurn.Timestamp,
                sourceTurn.SessionId,
                sourceTurn.Turn,
                sourceTurn.UserMessage,
                sourceTurn.AssistantMessage,
                Usage: turnUsage);

            await ConversationLogFile.AppendAsync(logPath, entry);
            memory = memoryUpdate.Memory.Normalize();

            await MemoryStoreFile.SaveAsync(memoryPath, memory);
            await SaveSessionAsync(agent, session, sessionPath);

            cumulativeTokens = turnUsage.CumulativeTotalTokens;

            Console.WriteLine(
                $"Tokens > this turn: {turnUsage.TurnTotalTokens} " +
                $"(dialogue {turnUsage.Dialogue.TotalTokenCount}, memory {turnUsage.MemoryUpdate.TotalTokenCount}) | " +
                $"cumulative: {turnUsage.CumulativeTotalTokens}");
            Console.WriteLine();

            turn++;
        }
    }

    private static async Task<AgentSession> RestoreSessionAsync(AIAgent agent, string sessionPath)
    {
        if (!File.Exists(sessionPath))
        {
            return await agent.CreateSessionAsync();
        }

        try
        {
            string serialized = await File.ReadAllTextAsync(sessionPath);
            if (string.IsNullOrWhiteSpace(serialized))
            {
                return await agent.CreateSessionAsync();
            }

            JsonDocument doc = JsonDocument.Parse(serialized);
            return await agent.DeserializeSessionAsync(doc.RootElement);
        }
        catch
        {
            return await agent.CreateSessionAsync();
        }
    }

    private static async Task SaveSessionAsync(AIAgent agent, AgentSession session, string sessionPath)
    {
        JsonElement serialized = await agent.SerializeSessionAsync(session);
        await File.WriteAllTextAsync(sessionPath, serialized.GetRawText(), Utf8NoBom);
    }
}

internal static class SessionIdentity
{
    public static string Get(AgentSession session)
    {
        if (session is ChatClientAgentSession chatSession &&
            !string.IsNullOrWhiteSpace(chatSession.ConversationId))
        {
            return chatSession.ConversationId;
        }

        return "local-session";
    }
}

internal static class MemoryPromptComposer
{
    public static string Compose(MemoryStore memory, string userInput)
    {
        if (memory.IsEmpty)
        {
            return userInput;
        }

        var sb = new StringBuilder();
        sb.AppendLine("以下是之前整理好的摘要記憶，請把它當成補充上下文：");
        sb.AppendLine(JsonSerializer.Serialize(memory, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }));
        sb.AppendLine();
        sb.AppendLine("本回合使用者輸入：");
        sb.Append(userInput);
        return sb.ToString();
    }
}

internal static class ResponseTextExtractor
{
    public static string Extract(object response) => response.ToString()?.Trim() ?? string.Empty;
}

internal static class MemorySummarizer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static async Task<MemoryUpdateResult> UpdateAsync(
        AIAgent memoryAgent,
        MemoryStore currentMemory,
        ConversationTurnLog turn)
    {
        try
        {
            string prompt = $$"""
                舊的摘要記憶：
                {{JsonSerializer.Serialize(currentMemory, JsonOptions)}}

                本回合 user 訊息：
                {{turn.UserMessage}}

                本回合 assistant 訊息：
                {{turn.AssistantMessage}}
                """;

            AgentResponse<MemoryStore> response = await memoryAgent.RunAsync<MemoryStore>(
                prompt,
                null,
                JsonOptions);
            return new MemoryUpdateResult(response.Result.Normalize(), UsageSnapshot.From(response.Usage));
        }
        catch
        {
            return new MemoryUpdateResult(currentMemory, new UsageSnapshot(0, 0, 0));
        }
    }
}

internal static class ConversationLogFile
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly JsonSerializerOptions DisplayJsonOptions = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static async Task AppendAsync(string path, ConversationTurnLog entry)
    {
        string line = JsonSerializer.Serialize(entry) + Environment.NewLine;
        await File.AppendAllTextAsync(path, line, Utf8NoBom);
    }

    public static async Task<int> GetNextTurnNumberAsync(string path)
    {
        if (!File.Exists(path))
        {
            return 1;
        }

        string[] lines = await File.ReadAllLinesAsync(path);
        return lines.Length + 1;
    }

    public static async Task<long> GetCumulativeTotalTokensAsync(string path)
    {
        if (!File.Exists(path))
        {
            return 0;
        }

        string[] lines = await File.ReadAllLinesAsync(path);
        long total = 0;

        foreach (string line in lines.Where(static line => !string.IsNullOrWhiteSpace(line)))
        {
            try
            {
                ConversationTurnLog? entry = JsonSerializer.Deserialize<ConversationTurnLog>(line);
                total += entry?.Usage?.TurnTotalTokens ?? 0;
            }
            catch
            {
            }
        }

        return total;
    }

    public static async Task<string> ReadRecentTurnsAsync(string path, int count)
    {
        if (!File.Exists(path))
        {
            return string.Empty;
        }

        string[] lines = await File.ReadAllLinesAsync(path);
        IEnumerable<string> recent = lines.Where(static line => !string.IsNullOrWhiteSpace(line)).TakeLast(count);
        var rendered = new List<string>();

        foreach (string line in recent)
        {
            try
            {
                ConversationTurnLog? entry = JsonSerializer.Deserialize<ConversationTurnLog>(line);
                rendered.Add(entry is null
                    ? line
                    : JsonSerializer.Serialize(entry, DisplayJsonOptions));
            }
            catch
            {
                rendered.Add(line);
            }
        }

        return string.Join(Environment.NewLine, rendered);
    }
}

internal static class MemoryStoreFile
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static async Task<MemoryStore> LoadAsync(string path)
    {
        if (!File.Exists(path))
        {
            return MemoryStore.CreateEmpty();
        }

        try
        {
            string json = await File.ReadAllTextAsync(path);
            return (JsonSerializer.Deserialize<MemoryStore>(json) ?? MemoryStore.CreateEmpty()).Normalize();
        }
        catch
        {
            string backupPath = $"{path}.broken-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
            File.Copy(path, backupPath, overwrite: true);
            return MemoryStore.CreateEmpty();
        }
    }

    public static Task SaveAsync(string path, MemoryStore store)
    {
        string json = JsonSerializer.Serialize(store, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
        return File.WriteAllTextAsync(path, json, Utf8NoBom);
    }
}

internal static class FileHelpers
{
    public static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

internal sealed record ConversationTurnLog(
    DateTimeOffset Timestamp,
    string SessionId,
    int Turn,
    string UserMessage,
    string AssistantMessage,
    TurnTokenUsage? Usage = null);

internal sealed record MemoryUpdateResult(
    MemoryStore Memory,
    UsageSnapshot Usage);

internal sealed record UsageSnapshot(
    long InputTokenCount,
    long OutputTokenCount,
    long TotalTokenCount)
{
    public static UsageSnapshot From(UsageDetails? usage) =>
        usage is null
            ? new UsageSnapshot(0, 0, 0)
            : new UsageSnapshot(
                usage.InputTokenCount ?? 0,
                usage.OutputTokenCount ?? 0,
                usage.TotalTokenCount ?? ((usage.InputTokenCount ?? 0) + (usage.OutputTokenCount ?? 0)));
}

internal sealed record TurnTokenUsage(
    UsageSnapshot Dialogue,
    UsageSnapshot MemoryUpdate,
    long TurnTotalTokens,
    long CumulativeTotalTokens)
{
    public static TurnTokenUsage Create(UsageSnapshot dialogue, UsageSnapshot memoryUpdate, long currentCumulative)
    {
        long turnTotal = dialogue.TotalTokenCount + memoryUpdate.TotalTokenCount;
        return new TurnTokenUsage(
            dialogue,
            memoryUpdate,
            turnTotal,
            currentCumulative + turnTotal);
    }
}

internal sealed record MemoryStore(
    MemoryProfile Profile,
    List<string> Preferences,
    List<string> ImportantTopics,
    List<string> OpenLoops,
    string ConversationSummary)
{
    [JsonIgnore]
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Profile?.Name) &&
        string.IsNullOrWhiteSpace(Profile?.Bio) &&
        (Preferences?.Count ?? 0) == 0 &&
        (ImportantTopics?.Count ?? 0) == 0 &&
        (OpenLoops?.Count ?? 0) == 0 &&
        string.IsNullOrWhiteSpace(ConversationSummary);

    public static MemoryStore CreateEmpty() =>
        new(
            new MemoryProfile(null, null),
            new List<string>(),
            new List<string>(),
            new List<string>(),
            string.Empty);

    public MemoryStore Normalize() =>
        new(
            Profile ?? new MemoryProfile(null, null),
            Preferences ?? new List<string>(),
            ImportantTopics ?? new List<string>(),
            OpenLoops ?? new List<string>(),
            ConversationSummary ?? string.Empty);
}

internal sealed record MemoryProfile(string? Name, string? Bio);
