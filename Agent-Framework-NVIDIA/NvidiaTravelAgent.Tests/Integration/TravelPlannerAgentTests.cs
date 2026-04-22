using FluentAssertions;
using Microsoft.Agents.AI;
using NvidiaTravelAgent.Agents;
using NvidiaTravelAgent.Models;
using NvidiaTravelAgent.Services;

namespace NvidiaTravelAgent.Tests.Integration;

public class TravelPlannerAgentTests
{
    [Fact]
    public async Task RunAsync_returns_plan_with_sources_and_persists_session_history()
    {
        var session = await CreateSessionAsync();
        var agent = CreateAgent();

        var response = await agent.RunAsync("請規劃三天兩夜台南美食散步行程", session);

        response.Text.Should().Contain("# 旅遊行程建議清單");
        response.Text.Should().Contain("## 資訊來源");
        session.TryGetInMemoryChatHistory(out var messages).Should().BeTrue();
        messages.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task ResetSession_clears_chat_history()
    {
        var session = await CreateSessionAsync();
        session.SetInMemoryChatHistory([
            new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.User, "hello")
        ]);

        TravelPlannerAgent.ResetSession(session);

        session.TryGetInMemoryChatHistory(out var messages).Should().BeTrue();
        messages.Should().BeEmpty();
    }

    private static async Task<AgentSession> CreateSessionAsync()
    {
        var agent = CreateAgent();
        return await agent.CreateSessionAsync();
    }

    private static TravelPlannerAgent CreateAgent()
    {
        var llm = new FakeNvidiaChatClient();
        var search = new FakeWebSearchService();
        var verifier = new FakeWebPageVerifier();
        var planner = new TravelPlannerEngine(llm, search, verifier, new ItineraryComposer());
        return new TravelPlannerAgent(planner);
    }

    private sealed class FakeNvidiaChatClient : INvidiaChatClient
    {
        public Task<T> CompleteJsonAsync<T>(IReadOnlyList<LlmMessage> messages, CancellationToken cancellationToken = default)
        {
            object result = typeof(T) == typeof(TravelRequest)
                ? new TravelRequest
                {
                    Destination = "台南",
                    Days = 3,
                    TravelStyle = "美食散步",
                    TransportationPreference = "大眾運輸",
                    Budget = "中等",
                    SpecialRequirements = ["想吃在地小吃"]
                }
                : new TravelPlan
                {
                    Summary = "以台南在地小吃與老城區散步為主的三天兩夜行程。",
                    DailyPlans =
                    [
                        new DailyPlan
                        {
                            Day = 1,
                            Theme = "國華街與周邊小吃",
                            Items =
                            [
                                new ItineraryItem
                                {
                                    Category = "餐飲",
                                    Name = "阿堂鹹粥",
                                    Description = "安排早午餐時段前往，體驗台南代表性鹹粥。"
                                }
                            ]
                        }
                    ],
                    TransportationNotes = ["建議搭配步行與公車移動。"],
                    AccommodationNotes = ["住宿可優先考慮台南車站或中西區周邊。"],
                    Cautions = ["熱門店家可能需要排隊，建議避開尖峰時段。"]
                };

            return Task.FromResult((T)result);
        }
    }

    private sealed class FakeWebSearchService : IWebSearchService
    {
        public Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<SearchResult> results =
            [
                new SearchResult("阿堂鹹粥", "https://example.com/food"),
                new SearchResult("台南車站", "https://example.com/transport")
            ];

            return Task.FromResult(results);
        }
    }

    private sealed class FakeWebPageVerifier : IWebPageVerifier
    {
        public Task<VerifiedSource> VerifyAsync(string url, CancellationToken cancellationToken = default)
        {
            var source = url.Contains("transport", StringComparison.OrdinalIgnoreCase)
                ? new VerifiedSource
                {
                    Url = url,
                    Title = "台南車站",
                    Summary = "台南交通資訊，可作為住宿與交通轉運節點。",
                    Facts = ["台南交通便利，適合安排住宿區域。"]
                }
                : new VerifiedSource
                {
                    Url = url,
                    Title = "阿堂鹹粥",
                    Summary = "台南在地美食與老字號小吃。",
                    Facts = ["阿堂鹹粥是台南在地美食熱門早午餐選項。"]
                };

            return Task.FromResult(source);
        }

        public Task<VerifiedSource> VerifyHtmlAsync(string url, string html, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
