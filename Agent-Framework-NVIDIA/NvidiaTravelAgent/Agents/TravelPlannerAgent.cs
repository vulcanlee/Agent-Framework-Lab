using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using NvidiaTravelAgent.Services;
using System.Runtime.CompilerServices;

namespace NvidiaTravelAgent.Agents;

public sealed class TravelPlannerAgent : AIAgent
{
    private readonly TravelPlannerEngine _planner;

    public TravelPlannerAgent(TravelPlannerEngine planner)
    {
        _planner = planner;
    }

    public static void ResetSession(AgentSession session) => session.SetInMemoryChatHistory([]);

    protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken = default)
    {
        var session = new TravelPlannerSession();
        session.SetInMemoryChatHistory([]);
        return ValueTask.FromResult<AgentSession>(session);
    }

    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
        System.Text.Json.JsonElement serializedState,
        System.Text.Json.JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult<AgentSession>(new TravelPlannerSession());
    }

    protected override ValueTask<System.Text.Json.JsonElement> SerializeSessionCoreAsync(
        AgentSession session,
        System.Text.Json.JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(System.Text.Json.JsonSerializer.SerializeToElement(session.StateBag, jsonSerializerOptions));
    }

    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        session ??= await CreateSessionAsync(cancellationToken);
        session.TryGetInMemoryChatHistory(out var history);
        history ??= [];

        var transcript = BuildTranscript(history, messages);
        var output = await _planner.PlanAsync(transcript, cancellationToken);

        var responseMessage = new ChatMessage(ChatRole.Assistant, output);
        var updatedHistory = history.Concat(messages).Append(responseMessage).ToList();
        session.SetInMemoryChatHistory(updatedHistory);

        return new AgentResponse(responseMessage);
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await RunCoreAsync(messages, session, options, cancellationToken);
        foreach (var update in response.ToAgentResponseUpdates())
        {
            yield return update;
        }
    }

    private static string BuildTranscript(IEnumerable<ChatMessage> history, IEnumerable<ChatMessage> incomingMessages)
    {
        return string.Join(
            Environment.NewLine,
            history.Concat(incomingMessages).Select(message => $"{message.Role.Value}: {message.Text}"));
    }

    private sealed class TravelPlannerSession : AgentSession
    {
        public TravelPlannerSession()
            : base()
        {
        }
    }
}
