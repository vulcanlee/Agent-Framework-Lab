using FluentAssertions;
using NvidiaTravelAgent.Models;
using NvidiaTravelAgent.Services;

namespace NvidiaTravelAgent.Tests.Services;

public class ItineraryComposerTests
{
    [Fact]
    public void Compose_throws_when_item_has_no_sources()
    {
        var plan = new TravelPlan
        {
            Summary = "台南在地小吃行程。",
            DailyPlans =
            [
                new DailyPlan
                {
                    Day = 1,
                    Theme = "早餐散步",
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
            ]
        };

        var composer = new ItineraryComposer();

        var action = () => composer.Compose(plan, []);

        action.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*阿堂鹹粥*");
    }
}
