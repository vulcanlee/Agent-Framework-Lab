using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Agent_Framework_Structure_Output.Models;

namespace Agent_Framework_Structure_Output.Services;

internal static class OutputRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string RenderJson(MeetingActionPlan plan) =>
        JsonSerializer.Serialize(plan, JsonOptions);

    public static string RenderSummary(MeetingActionPlan plan)
    {
        var builder = new StringBuilder();

        builder.AppendLine($"會議主題：{plan.MeetingTitle ?? "未提供"}");
        builder.AppendLine();
        builder.AppendLine("摘要");
        builder.AppendLine(plan.Summary ?? "未提供");
        builder.AppendLine();

        builder.AppendLine("關鍵決策");
        if (plan.KeyDecisions.Count == 0)
        {
            builder.AppendLine("- 無");
        }
        else
        {
            foreach (string decision in plan.KeyDecisions)
            {
                builder.AppendLine($"- {decision}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("行動項目");
        if (plan.ActionItems.Count == 0)
        {
            builder.AppendLine("- 無");
        }
        else
        {
            foreach (ActionItem item in plan.ActionItems)
            {
                builder.AppendLine($"- 工作：{item.Task ?? "待確認"}");
                builder.AppendLine($"  負責人：{item.Owner ?? "待確認"}");
                builder.AppendLine($"  截止日：{item.DueDate ?? "待確認"}");
                builder.AppendLine($"  優先級：{item.Priority ?? "待確認"}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("風險");
        if (plan.Risks.Count == 0)
        {
            builder.AppendLine("- 無");
        }
        else
        {
            foreach (RiskItem risk in plan.Risks)
            {
                builder.AppendLine($"- 風險：{risk.Risk ?? "待確認"}");
                builder.AppendLine($"  影響：{risk.Impact ?? "待確認"}");
                builder.AppendLine($"  建議處置：{risk.Mitigation ?? "待確認"}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("待確認問題");
        if (plan.FollowUpQuestions.Count == 0)
        {
            builder.AppendLine("- 無");
        }
        else
        {
            foreach (FollowUpQuestion question in plan.FollowUpQuestions)
            {
                builder.AppendLine($"- 問題：{question.Question ?? "待確認"}");
                builder.AppendLine($"  原因：{question.WhyItMatters ?? "待確認"}");
            }
        }

        return builder.ToString().TrimEnd();
    }
}
