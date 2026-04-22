using System.Text.Json;
using System.Text.Json.Serialization;

namespace NvidiaTravelAgent.Models;

public sealed class TravelRequest
{
    [JsonPropertyName("destination")]
    public string Destination { get; set; } = string.Empty;

    [JsonPropertyName("days")]
    public int Days { get; set; }

    [JsonPropertyName("travelStyle")]
    public string TravelStyle { get; set; } = string.Empty;

    [JsonPropertyName("transportationPreference")]
    public string TransportationPreference { get; set; } = string.Empty;

    [JsonPropertyName("budget")]
    public string Budget { get; set; } = string.Empty;

    [JsonPropertyName("specialRequirements")]
    public List<string> SpecialRequirements { get; set; } = [];
}

public static class TravelRequestSerializer
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
}
