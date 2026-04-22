using FluentAssertions;
using NvidiaTravelAgent.Models;
using System.Text.Json;

namespace NvidiaTravelAgent.Tests.Models;

public class TravelRequestTests
{
    [Fact]
    public void Structured_json_can_be_deserialized()
    {
        const string json = """
            {
              "destination": "台南",
              "days": 3,
              "travelStyle": "美食散步",
              "transportationPreference": "大眾運輸",
              "budget": "中等",
              "specialRequirements": [
                "不自駕",
                "喜歡老街與小吃"
              ]
            }
            """;

        var request = JsonSerializer.Deserialize<TravelRequest>(json, TravelRequestSerializer.Options);

        request.Should().NotBeNull();
        request!.Destination.Should().Be("台南");
        request.Days.Should().Be(3);
        request.SpecialRequirements.Should().Contain("不自駕");
    }
}
