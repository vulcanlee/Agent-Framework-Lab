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
            Summary = "三天兩夜台南散步旅行",
            DailyPlans =
            [
                new DailyPlan
                {
                    Day = 1,
                    Theme = "古蹟與小吃",
                    Items =
                    [
                        new ItineraryItem
                        {
                            Category = "景點",
                            Name = "赤崁樓",
                            Description = "上午造訪赤崁樓"
                        }
                    ]
                }
            ]
        };

        var composer = new ItineraryComposer();

        var action = () => composer.Compose(plan, []);

        action.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*赤崁樓*");
    }
}
