using System.Text.Json.Serialization;

namespace Agent_Framework_Structure_Output.Models;

public sealed class MeetingActionPlan
{
    [JsonPropertyName("meeting_title")]
    public string? MeetingTitle { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("key_decisions")]
    public List<string> KeyDecisions { get; set; } = [];

    [JsonPropertyName("action_items")]
    public List<ActionItem> ActionItems { get; set; } = [];

    [JsonPropertyName("risks")]
    public List<RiskItem> Risks { get; set; } = [];

    [JsonPropertyName("follow_up_questions")]
    public List<FollowUpQuestion> FollowUpQuestions { get; set; } = [];
}

public sealed class ActionItem
{
    [JsonPropertyName("task")]
    public string? Task { get; set; }

    [JsonPropertyName("owner")]
    public string? Owner { get; set; }

    [JsonPropertyName("due_date")]
    public string? DueDate { get; set; }

    [JsonPropertyName("priority")]
    public string? Priority { get; set; }
}

public sealed class RiskItem
{
    [JsonPropertyName("risk")]
    public string? Risk { get; set; }

    [JsonPropertyName("impact")]
    public string? Impact { get; set; }

    [JsonPropertyName("mitigation")]
    public string? Mitigation { get; set; }
}

public sealed class FollowUpQuestion
{
    [JsonPropertyName("question")]
    public string? Question { get; set; }

    [JsonPropertyName("why_it_matters")]
    public string? WhyItMatters { get; set; }
}
