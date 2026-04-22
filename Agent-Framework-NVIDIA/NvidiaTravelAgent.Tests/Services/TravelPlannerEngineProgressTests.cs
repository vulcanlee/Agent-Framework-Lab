using FluentAssertions;
using NvidiaTravelAgent.Models;
using NvidiaTravelAgent.Progress;
using NvidiaTravelAgent.Services;

namespace NvidiaTravelAgent.Tests.Services;

public class TravelPlannerEngineProgressTests
{
    [Fact]
    public async Task PlanAsync_reports_direct_mode_progress_in_order()
    {
        var reporter = new RecordingProgressReporter();
        var engine = CreateEngine(
            reporter,
            new TravelRequest
            {
                Destination = "台南",
                Days = 3,
                TravelStyle = "美食散步",
                TransportationPreference = "大眾運輸",
                Budget = "中等",
                SpecialRequirements = ["想吃在地小吃"]
            });

        await engine.PlanAsync("三天兩夜台南美食散步行程");

        reporter.Updates.Select(update => update.Stage).Should().ContainInOrder(
            ProgressStage.AnalyzingPrompt,
            ProgressStage.ChoosingStrategy,
            ProgressStage.PlanningResearch,
            ProgressStage.SearchingWeb,
            ProgressStage.VerifyingSources,
            ProgressStage.SynthesizingItinerary,
            ProgressStage.FormattingChecklist,
            ProgressStage.Completed);
    }

    [Fact]
    public async Task PlanAsync_reports_iterative_strategy_for_complex_requests()
    {
        var reporter = new RecordingProgressReporter();
        var engine = CreateEngine(
            reporter,
            new TravelRequest
            {
                Destination = "香港",
                Days = 3,
                TravelStyle = "在地美食",
                TransportationPreference = "大眾運輸",
                Budget = "中高",
                SpecialRequirements =
                [
                    "想找平民餐飲",
                    "需要比較多家蛋塔",
                    "希望知道推薦理由"
                ]
            });

        await engine.PlanAsync("三天兩夜去香港旅遊，想體驗當地平民餐飲，還需要比較多家蛋塔並整理推薦理由。");

        reporter.Updates.Should().Contain(update =>
            update.Stage == ProgressStage.ChoosingStrategy &&
            update.Message.Contains("分步整理候選建議"));
    }

    [Fact]
    public async Task PlanAsync_reports_clarifying_stage_when_request_is_missing_key_fields()
    {
        var reporter = new RecordingProgressReporter();
        var engine = CreateEngine(
            reporter,
            new TravelRequest
            {
                Destination = "",
                Days = 0,
                TravelStyle = "自由行",
                TransportationPreference = "大眾運輸",
                Budget = "中等",
                SpecialRequirements = []
            });

        await engine.PlanAsync("幫我安排一下旅行");

        reporter.Updates.Should().Contain(update => update.Stage == ProgressStage.ClarifyingMissingInfo);
    }

    [Fact]
    public async Task PlanAsync_reports_failed_stage_when_search_throws()
    {
        var reporter = new RecordingProgressReporter();
        var engine = new TravelPlannerEngine(
            new FakeNvidiaChatClient(
                new TravelRequest
                {
                    Destination = "香港",
                    Days = 3,
                    TravelStyle = "在地美食",
                    TransportationPreference = "大眾運輸",
                    Budget = "中等",
                    SpecialRequirements = ["想吃蛋塔"]
                },
                CreatePlan()),
            new ThrowingWebSearchService(),
            new FakeWebPageVerifier(),
            new ItineraryComposer(),
            reporter);

        var action = () => engine.PlanAsync("請規劃香港蛋塔之旅");

        await action.Should().ThrowAsync<InvalidOperationException>();
        reporter.Updates.Should().Contain(update => update.Stage == ProgressStage.Failed);
    }

    private static TravelPlannerEngine CreateEngine(RecordingProgressReporter reporter, TravelRequest request)
    {
        return new TravelPlannerEngine(
            new FakeNvidiaChatClient(request, CreatePlan()),
            new FakeWebSearchService(),
            new FakeWebPageVerifier(),
            new ItineraryComposer(),
            reporter);
    }

    private static TravelPlan CreatePlan()
    {
        return new TravelPlan
        {
            Summary = "示範旅遊行程。",
            DailyPlans =
            [
                new DailyPlan
                {
                    Day = 1,
                    Theme = "在地小吃",
                    Items =
                    [
                        new ItineraryItem
                        {
                            Category = "餐飲",
                            Name = "阿堂鹹粥",
                            Description = "安排早餐時段前往。"
                        }
                    ]
                }
            ],
            TransportationNotes = ["建議搭配步行與大眾運輸。"],
            AccommodationNotes = ["住宿可優先考慮市中心。"],
            Cautions = ["熱門店家可能需要排隊。"]
        };
    }

    private sealed class RecordingProgressReporter : IProgressReporter
    {
        public List<ProgressUpdate> Updates { get; } = [];

        public void Report(ProgressUpdate update)
        {
            Updates.Add(update);
        }
    }

    private sealed class FakeNvidiaChatClient : INvidiaChatClient
    {
        private readonly TravelRequest _request;
        private readonly TravelPlan _plan;

        public FakeNvidiaChatClient(TravelRequest request, TravelPlan plan)
        {
            _request = request;
            _plan = plan;
        }

        public Task<T> CompleteJsonAsync<T>(IReadOnlyList<LlmMessage> messages, CancellationToken cancellationToken = default)
        {
            object result = typeof(T) == typeof(TravelRequest) ? _request : _plan;
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

    private sealed class ThrowingWebSearchService : IWebSearchService
    {
        public Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("搜尋失敗");
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
                    Summary = "可作為住宿與交通轉運節點。",
                    Facts = ["交通便利，適合安排住宿區域。"]
                }
                : new VerifiedSource
                {
                    Url = url,
                    Title = "阿堂鹹粥",
                    Summary = "台南在地知名小吃。",
                    Facts = ["阿堂鹹粥是台南熱門早午餐選項。"]
                };

            return Task.FromResult(source);
        }

        public Task<VerifiedSource> VerifyHtmlAsync(string url, string html, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
