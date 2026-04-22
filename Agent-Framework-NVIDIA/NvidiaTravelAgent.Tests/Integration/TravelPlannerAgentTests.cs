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

        var response = await agent.RunAsync("三天兩夜台南美食散步，不自駕", session);

        response.Text.Should().Contain("來源清單");
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
                    SpecialRequirements = ["不自駕"]
                }
                : new TravelPlan
                {
                    Summary = "三天兩夜台南美食散步旅行",
                    DailyPlans =
                    [
                        new DailyPlan
                        {
                            Day = 1,
                            Theme = "古蹟散步",
                            Items =
                            [
                                new ItineraryItem
                                {
                                    Category = "景點",
                                    Name = "赤崁樓",
                                    Description = "上午從赤崁樓開始"
                                }
                            ]
                        }
                    ],
                    TransportationNotes = ["步行與公車為主"],
                    AccommodationNotes = ["建議住台南火車站周邊"],
                    Cautions = ["旺季需提早訂房"]
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
                new SearchResult("台南旅遊網", "https://www.twtainan.net/attractions/detail/123"),
                new SearchResult("台鐵", "https://www.railway.gov.tw/tra-tip-web/tip")
            ];

            return Task.FromResult(results);
        }
    }

    private sealed class FakeWebPageVerifier : IWebPageVerifier
    {
        public Task<VerifiedSource> VerifyAsync(string url, CancellationToken cancellationToken = default)
        {
            var source = new VerifiedSource
            {
                Url = url,
                Title = url.Contains("railway", StringComparison.OrdinalIgnoreCase) ? "台鐵官網" : "台南旅遊網",
                Summary = "已驗證的旅遊資料",
                Facts =
                [
                    url.Contains("railway", StringComparison.OrdinalIgnoreCase)
                        ? "可搭乘台鐵進出台南"
                        : "赤崁樓位於台南市中西區"
                ]
            };

            return Task.FromResult(source);
        }

        public Task<VerifiedSource> VerifyHtmlAsync(string url, string html, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
