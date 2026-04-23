using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentFrameworkPersistenceMemory.Agent;

internal sealed class PmAgentFrameworkAgent(
    string displayName,
    Func<string, CancellationToken, Task<string>> completionHandler) : AIAgent
{
    private static readonly JsonElement EmptySessionState = JsonDocument.Parse("{}").RootElement.Clone();
    private readonly Func<string, CancellationToken, Task<string>> _completionHandler = completionHandler;
    private readonly ConditionalWeakTable<AgentSession, List<ChatMessage>> _history = new();

    public override string Name { get; } = displayName;

    protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken = default)
        => new(new PmAgentSession());

    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
        JsonElement serializedState,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
        => new(new PmAgentSession());

    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
        AgentSession session,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
        => new(EmptySessionState);

    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        session ??= await CreateSessionAsync(cancellationToken);
        var incomingMessages = messages.ToList();
        var historyMessages = _history.GetValue(session, _ => []);

        var prompt = string.Join(Environment.NewLine + Environment.NewLine, historyMessages.Concat(incomingMessages).Select(FormatMessage));
        var completion = await _completionHandler(prompt, cancellationToken);
        var responseMessage = new ChatMessage(ChatRole.Assistant, completion)
        {
            MessageId = Guid.NewGuid().ToString("N"),
            AuthorName = Name
        };

        historyMessages.AddRange(incomingMessages);
        historyMessages.Add(responseMessage);

        return new AgentResponse
        {
            AgentId = Id,
            ResponseId = Guid.NewGuid().ToString("N"),
            Messages = [responseMessage]
        };
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await RunCoreAsync(messages, session, options, cancellationToken);
        foreach (var message in response.Messages)
        {
            yield return new AgentResponseUpdate
            {
                AgentId = Id,
                ResponseId = response.ResponseId,
                MessageId = message.MessageId ?? Guid.NewGuid().ToString("N"),
                AuthorName = Name,
                Role = ChatRole.Assistant,
                Contents = message.Contents
            };
        }
    }

    private static string FormatMessage(ChatMessage message)
    {
        var text = string.Join(" ", message.Contents.OfType<TextContent>().Select(content => content.Text));
        return $"{message.Role}: {text}";
    }
}
