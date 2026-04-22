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
              "destination": "香港",
              "days": 3,
              "travelStyle": "在地美食與散步",
              "transportationPreference": "大眾運輸",
              "budget": "中高",
              "specialRequirements": [
                "想吃平民餐飲",
                "需要購買蛋塔外帶回飯店"
              ]
            }
            """;

        var request = JsonSerializer.Deserialize<TravelRequest>(json, TravelRequestSerializer.Options);

        request.Should().NotBeNull();
        request!.Destination.Should().Be("香港");
        request.Days.Should().Be(3);
        request.SpecialRequirements.Should().Contain("需要購買蛋塔外帶回飯店");
    }
}
