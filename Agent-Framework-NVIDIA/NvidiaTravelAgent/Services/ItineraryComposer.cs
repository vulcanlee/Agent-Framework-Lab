using NvidiaTravelAgent.Models;
using System.Text;

namespace NvidiaTravelAgent.Services;

public sealed class ItineraryComposer
{
    public string Compose(TravelPlan plan, IReadOnlyCollection<VerifiedSource> sources)
    {
        Validate(plan, sources);

        var builder = new StringBuilder();
        builder.AppendLine("# 旅遊行程建議清單");
        builder.AppendLine();
        builder.AppendLine("## 行程摘要");
        builder.AppendLine(plan.Summary);
        builder.AppendLine();

        builder.AppendLine("## 每日安排");
        foreach (var day in plan.DailyPlans.OrderBy(x => x.Day))
        {
            builder.AppendLine($"### Day {day.Day} - {day.Theme}");
            foreach (var item in day.Items)
            {
                builder.AppendLine($"- [{item.Category}] {item.Name}：{item.Description}");
            }

            builder.AppendLine();
        }

        builder.AppendLine("## 交通建議");
        foreach (var note in plan.TransportationNotes)
        {
            builder.AppendLine($"- {note}");
        }

        builder.AppendLine();
        builder.AppendLine("## 住宿建議");
        foreach (var note in plan.AccommodationNotes)
        {
            builder.AppendLine($"- {note}");
        }

        builder.AppendLine();
        builder.AppendLine("## 注意事項 / 待確認");
        foreach (var note in plan.Cautions)
        {
            builder.AppendLine($"- {note}");
        }

        builder.AppendLine();
        builder.AppendLine("## 資訊來源");
        foreach (var source in sources)
        {
            builder.AppendLine($"- {source.Title} - {source.Url}");
        }

        return builder.ToString().TrimEnd();
    }

    public string ComposeFallback(TravelRequest request, IReadOnlyCollection<VerifiedSource> sources)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# 旅遊行程建議清單");
        builder.AppendLine();
        builder.AppendLine("## 行程摘要");
        builder.AppendLine($"根據已驗證來源整理的 {request.Destination} {request.Days} 天旅遊建議清單。");
        builder.AppendLine();

        builder.AppendLine("## 規劃假設");
        builder.AppendLine($"- 旅遊風格：{request.TravelStyle}");
        builder.AppendLine($"- 交通偏好：{request.TransportationPreference}");
        builder.AppendLine($"- 預算：{request.Budget}");
        foreach (var requirement in request.SpecialRequirements)
        {
            builder.AppendLine($"- 特別需求：{requirement}");
        }

        builder.AppendLine();
        builder.AppendLine("## 已驗證候選建議");
        foreach (var source in sources.Take(10))
        {
            builder.AppendLine($"### {source.Title}");

            if (!string.IsNullOrWhiteSpace(source.Summary))
            {
                builder.AppendLine($"- 摘要：{source.Summary}");
            }

            foreach (var fact in source.Facts.Take(4))
            {
                builder.AppendLine($"- 資訊重點：{fact}");
            }

            builder.AppendLine($"- 來源：{source.Url}");
            builder.AppendLine();
        }

        builder.AppendLine("## 注意事項 / 待確認");
        builder.AppendLine("- 這份結果由已驗證來源直接整理而成，因模型結構化行程失敗，暫時未排成逐日細部動線。");
        builder.AppendLine("- 建議先從上方已驗證候選清單挑選餐飲、蛋塔與交通資訊，再進一步細排每日順序。");
        builder.AppendLine();

        builder.AppendLine("## 資訊來源");
        foreach (var source in sources)
        {
            builder.AppendLine($"- {source.Title} - {source.Url}");
        }

        return builder.ToString().TrimEnd();
    }

    private static void Validate(TravelPlan plan, IReadOnlyCollection<VerifiedSource> sources)
    {
        foreach (var item in plan.DailyPlans.SelectMany(x => x.Items))
        {
            var matched = sources.Any(source =>
                source.Title.Contains(item.Name, StringComparison.OrdinalIgnoreCase) ||
                source.Summary.Contains(item.Name, StringComparison.OrdinalIgnoreCase) ||
                source.Facts.Any(fact => fact.Contains(item.Name, StringComparison.OrdinalIgnoreCase)));

            if (!matched)
            {
                throw new InvalidOperationException($"行程中的項目「{item.Name}」找不到對應來源，無法輸出最終建議。");
            }
        }
    }
}
