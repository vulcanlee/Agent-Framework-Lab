using FluentAssertions;
using Microsoft.Agents.AI;
using NvidiaTravelAgent.Agents;
using NvidiaTravelAgent.Configuration;
using NvidiaTravelAgent.Models;
using NvidiaTravelAgent.Progress;
using NvidiaTravelAgent.Services;
using System.Text;

namespace NvidiaTravelAgent.Tests.Cli;

public class CliApplicationTests
{
    [Fact]
    public async Task RunAsync_without_arguments_enters_repl_and_handles_exit()
    {
        var console = new FakeConsole("exit");

        var exitCode = await CliApplication.RunAsync(Array.Empty<string>(), console, CreateAgent, CancellationToken.None);

        exitCode.Should().Be(0);
        console.Output.Should().Contain("NVIDIA Travel Agent REPL");
        console.Output.Should().Contain("輸入需求開始規劃");
    }

    [Fact]
    public async Task RunAsync_with_help_argument_prints_help_instead_of_repl()
    {
        var console = new FakeConsole();

        var exitCode = await CliApplication.RunAsync(["help"], console, CreateAgent, CancellationToken.None);

        exitCode.Should().Be(0);
        console.Output.Should().Contain("用法：");
        console.Output.Should().NotContain("NVIDIA Travel Agent REPL");
    }

    [Fact]
    public async Task RunAsync_without_interactive_input_prints_guidance_message()
    {
        var console = new FakeConsole([null]);

        var exitCode = await CliApplication.RunAsync(Array.Empty<string>(), console, CreateAgent, CancellationToken.None);

        exitCode.Should().Be(1);
        console.Error.Should().Contain("沒有可互動的標準輸入");
    }

    [Fact]
    public async Task RunAsync_with_plan_request_prints_progress_and_final_result()
    {
        var console = new FakeConsole();

        var exitCode = await CliApplication.RunAsync(
            ["plan", "--request", "三天兩夜台南小吃之旅"],
            console,
            CreateAgent,
            CancellationToken.None);

        exitCode.Should().Be(0);
        console.Output.Should().Contain("[進度] 正在分析旅遊需求...");
        console.Output.Should().Contain("[進度] 正在整理旅遊行程建議清單...");
        console.Output.Should().Contain("# 旅遊行程建議清單");
    }

    [Fact]
    public async Task RunAsync_repl_continues_after_model_output_error_until_user_exits()
    {
        var console = new FakeConsole("請規劃香港三天兩夜美食行程", "exit");

        var exitCode = await CliApplication.RunAsync(
            Array.Empty<string>(),
            console,
            (options, progress) => CreateAgent(options, progress, new ThrowingNvidiaChatClient()),
            CancellationToken.None);

        exitCode.Should().Be(0);
        console.Output.Should().Contain("[進度] 行程生成失敗，正在整理錯誤原因...");
        console.Error.Should().Contain("需求解析失敗");
    }

    private static TravelPlannerAgent CreateAgent(AppOptions options, IProgressReporter progressReporter)
        => CreateAgent(options, progressReporter, null);

    private static TravelPlannerAgent CreateAgent(
        AppOptions options,
        IProgressReporter progressReporter,
        INvidiaChatClient? llm)
    {
        llm ??= new FakeNvidiaChatClient();
        var search = new FakeWebSearchService();
        var verifier = new FakeWebPageVerifier();
        var planner = new TravelPlannerEngine(llm, search, verifier, new ItineraryComposer(), progressReporter);
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

    private sealed class ThrowingNvidiaChatClient : INvidiaChatClient
    {
        public Task<T> CompleteJsonAsync<T>(IReadOnlyList<LlmMessage> messages, CancellationToken cancellationToken = default)
            => throw new ModelOutputException("需求解析失敗，請稍微簡化描述或重試。");
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
