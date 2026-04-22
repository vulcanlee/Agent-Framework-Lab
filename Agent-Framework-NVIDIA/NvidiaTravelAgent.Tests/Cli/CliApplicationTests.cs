using FluentAssertions;
using Microsoft.Agents.AI;
using NvidiaTravelAgent.Agents;
using NvidiaTravelAgent.Models;
using NvidiaTravelAgent.Services;
using System.Text;

namespace NvidiaTravelAgent.Tests.Cli;

public class CliApplicationTests
{
    [Fact]
    public async Task RunAsync_without_arguments_enters_repl_and_handles_exit()
    {
        var console = new FakeConsole("exit");
        var agent = CreateAgent();

        var exitCode = await CliApplication.RunAsync(Array.Empty<string>(), console, _ => agent, CancellationToken.None);

        exitCode.Should().Be(0);
        console.Output.Should().Contain("NVIDIA Travel Agent REPL");
        console.Output.Should().Contain("輸入需求開始規劃");
    }

    [Fact]
    public async Task RunAsync_with_help_argument_prints_help_instead_of_repl()
    {
        var console = new FakeConsole();

        var exitCode = await CliApplication.RunAsync(["help"], console, _ => CreateAgent(), CancellationToken.None);

        exitCode.Should().Be(0);
        console.Output.Should().Contain("用法");
        console.Output.Should().NotContain("NVIDIA Travel Agent REPL");
    }

    [Fact]
    public async Task RunAsync_without_interactive_input_prints_guidance_message()
    {
        var console = new FakeConsole([null]);

        var exitCode = await CliApplication.RunAsync(Array.Empty<string>(), console, _ => CreateAgent(), CancellationToken.None);

        exitCode.Should().Be(1);
        console.Error.Should().Contain("沒有可互動的標準輸入");
    }

    [Fact]
    public async Task RunAsync_with_plan_request_keeps_cli_behavior()
    {
        var console = new FakeConsole();

        var exitCode = await CliApplication.RunAsync(
            ["plan", "--request", "三天兩夜台南美食散步，不自駕"],
            console,
            _ => CreateAgent(),
            CancellationToken.None);

        exitCode.Should().Be(0);
        console.Output.Should().Contain("來源清單");
    }

    [Fact]
    public async Task RunAsync_repl_continues_after_model_output_error_until_user_exits()
    {
        var console = new FakeConsole("香港美食需求", "exit");
        var agent = CreateAgent(new ThrowingNvidiaChatClient());

        var exitCode = await CliApplication.RunAsync(Array.Empty<string>(), console, _ => agent, CancellationToken.None);

        exitCode.Should().Be(0);
        console.Error.Should().Contain("需求解析失敗");
    }

    private static TravelPlannerAgent CreateAgent(INvidiaChatClient? llm = null)
    {
        llm ??= new FakeNvidiaChatClient();
        var search = new FakeWebSearchService();
        var verifier = new FakeWebPageVerifier();
        var planner = new TravelPlannerEngine(llm, search, verifier, new ItineraryComposer());
        return new TravelPlannerAgent(planner);
    }

    private sealed class FakeConsole : IAppConsole
    {
        private readonly Queue<string?> _inputs;
        private readonly StringBuilder _output = new();
        private readonly StringBuilder _error = new();

        public FakeConsole(params string?[] inputs)
        {
            _inputs = new Queue<string?>(inputs ?? []);
        }

        public Encoding InputEncoding { get; set; } = Encoding.UTF8;
        public Encoding OutputEncoding { get; set; } = Encoding.UTF8;
        public string Output => _output.ToString();
        public string Error => _error.ToString();

        public string? ReadLine() => _inputs.Count > 0 ? _inputs.Dequeue() : null;
        public void Write(string value) => _output.Append(value);
        public void WriteLine(string value) => _output.AppendLine(value);
        public void WriteErrorLine(string value) => _error.AppendLine(value);
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

    private sealed class ThrowingNvidiaChatClient : INvidiaChatClient
    {
        public Task<T> CompleteJsonAsync<T>(IReadOnlyList<LlmMessage> messages, CancellationToken cancellationToken = default)
            => throw new ModelOutputException("需求解析失敗，請稍微簡化描述後重試。");
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
