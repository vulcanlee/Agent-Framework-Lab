using System.Text.Json.Serialization;

namespace NvidiaTravelAgent.Models;

public sealed class TravelPlan
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("dailyPlans")]
    public List<DailyPlan> DailyPlans { get; set; } = [];

    [JsonPropertyName("transportationNotes")]
    public List<string> TransportationNotes { get; set; } = [];

    [JsonPropertyName("accommodationNotes")]
    public List<string> AccommodationNotes { get; set; } = [];

    [JsonPropertyName("cautions")]
    public List<string> Cautions { get; set; } = [];
}

public sealed class DailyPlan
{
    [JsonPropertyName("day")]
    public int Day { get; set; }

    [JsonPropertyName("theme")]
    public string Theme { get; set; } = string.Empty;

    [JsonPropertyName("items")]
    public List<ItineraryItem> Items { get; set; } = [];
}

public sealed class ItineraryItem
{
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}
