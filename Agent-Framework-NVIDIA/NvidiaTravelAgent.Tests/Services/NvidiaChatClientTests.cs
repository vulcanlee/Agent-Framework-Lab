using FluentAssertions;
using NvidiaTravelAgent.Configuration;
using NvidiaTravelAgent.Models;
using NvidiaTravelAgent.Services;
using System.Net;
using System.Text;
using System.Text.Json;

namespace NvidiaTravelAgent.Tests.Services;

public class NvidiaChatClientTests
{
    [Fact]
    public async Task CompleteJsonAsync_normalizes_special_requirements_string_to_list()
    {
        var client = CreateClient("""
            {
              "destination": "香港",
              "days": 3,
              "travelStyle": "在地美食",
              "transportationPreference": "大眾運輸",
              "budget": "中等",
              "specialRequirements": "需要購買蛋塔"
            }
            """);

        var result = await client.CompleteJsonAsync<TravelRequest>(CreateMessages());

        result.SpecialRequirements.Should().Equal("需要購買蛋塔");
    }

    [Fact]
    public async Task CompleteJsonAsync_normalizes_special_requirements_object_to_list()
    {
        var client = CreateClient("""
            {
              "destination": "香港",
              "days": 3,
              "travelStyle": "在地美食",
              "transportationPreference": "大眾運輸",
              "budget": "中等",
              "specialRequirements": {
                "focus": "蛋塔",
                "note": "想外帶回飯店"
              }
            }
            """);

        var result = await client.CompleteJsonAsync<TravelRequest>(CreateMessages());

        result.SpecialRequirements.Should().ContainSingle();
        result.SpecialRequirements[0].Should().Contain("蛋塔");
        result.SpecialRequirements[0].Should().Contain("外帶回飯店");
    }

    [Fact]
    public async Task CompleteJsonAsync_normalizes_travel_plan_string_lists()
    {
        var client = CreateClient("""
            {
              "summary": "香港三天兩夜在地美食行程",
              "dailyPlans": [
                {
                  "day": 1,
                  "theme": "港島老店",
                  "items": null
                }
              ],
              "transportationNotes": "建議使用港鐵移動",
              "accommodationNotes": "住宿可考慮尖沙咀一帶",
              "cautions": "熱門店家需要排隊"
            }
            """);

        var result = await client.CompleteJsonAsync<TravelPlan>(CreateMessages());

        result.TransportationNotes.Should().Equal("建議使用港鐵移動");
        result.AccommodationNotes.Should().Equal("住宿可考慮尖沙咀一帶");
        result.Cautions.Should().Equal("熱門店家需要排隊");
        result.DailyPlans[0].Items.Should().BeEmpty();
    }

    [Fact]
    public async Task CompleteJsonAsync_retries_once_when_first_response_is_missing_required_fields()
    {
        var client = CreateClient(
            """
            {
              "days": 3,
              "travelStyle": "在地美食",
              "transportationPreference": "大眾運輸",
              "budget": "中等",
              "specialRequirements": "需要購買蛋塔"
            }
            """,
            """
            {
              "destination": "香港",
              "days": 3,
              "travelStyle": "在地美食",
              "transportationPreference": "大眾運輸",
              "budget": "中等",
              "specialRequirements": ["需要購買蛋塔"]
            }
            """);

        var result = await client.CompleteJsonAsync<TravelRequest>(CreateMessages());

        result.Destination.Should().Be("香港");
        result.SpecialRequirements.Should().Equal("需要購買蛋塔");
    }

    [Fact]
    public async Task CompleteJsonAsync_wraps_failure_after_retry_in_model_output_exception()
    {
        var client = CreateClient(
            """
            {
              "days": 0,
              "travelStyle": "",
              "transportationPreference": "大眾運輸",
              "budget": "中等"
            }
            """,
            """
            {
              "days": 0,
              "travelStyle": "",
              "transportationPreference": "大眾運輸",
              "budget": "中等"
            }
            """);

        var action = () => client.CompleteJsonAsync<TravelRequest>(CreateMessages());

        await action.Should().ThrowAsync<ModelOutputException>()
            .WithMessage("*需求解析失敗*");
    }

    private static NvidiaChatClient CreateClient(params string[] messageContents)
    {
        var handler = new QueueMessageHandler(messageContents);
        var httpClient = new HttpClient(handler);
        var options = new AppOptions
        {
            NvidiaApiKey = "test-key",
            BaseUri = new Uri("https://example.com/v1/")
        };

        return new NvidiaChatClient(httpClient, options);
    }

    private static IReadOnlyList<LlmMessage> CreateMessages() =>
    [
        new LlmMessage("system", "請輸出 JSON"),
        new LlmMessage("user", "請整理旅遊需求")
    ];

    private sealed class QueueMessageHandler : HttpMessageHandler
    {
        private readonly Queue<string> _contents;
        private string? _lastContent;

        public QueueMessageHandler(IEnumerable<string> contents)
        {
            _contents = new Queue<string>(contents);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = _contents.Count > 0 ? _contents.Dequeue() : _lastContent ?? "{}";
            _lastContent = content;
            var payload = new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            content
                        }
                    }
                }
            };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            });
        }
    }
}
