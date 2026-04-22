using NvidiaTravelAgent.Models;
using NvidiaTravelAgent.Progress;
using System.Text.Json;

namespace NvidiaTravelAgent.Services;

public sealed class TravelPlannerEngine
{
    private readonly INvidiaChatClient _llm;
    private readonly IWebSearchService _searchService;
    private readonly IWebPageVerifier _pageVerifier;
    private readonly ItineraryComposer _composer;
    private readonly IProgressReporter _progressReporter;

    public TravelPlannerEngine(
        INvidiaChatClient llm,
        IWebSearchService searchService,
        IWebPageVerifier pageVerifier,
        ItineraryComposer composer,
        IProgressReporter? progressReporter = null)
    {
        _llm = llm;
        _searchService = searchService;
        _pageVerifier = pageVerifier;
        _composer = composer;
        _progressReporter = progressReporter ?? NullProgressReporter.Instance;
    }

    public async Task<string> PlanAsync(string conversation, CancellationToken cancellationToken = default)
    {
        try
        {
            Report(ProgressStage.AnalyzingPrompt, "正在分析旅遊需求...");

            var request = await ParseRequestAsync(conversation, cancellationToken);
            ReportStrategy(ChoosePlanningMode(request, conversation));

            Report(ProgressStage.PlanningResearch, "正在整理搜尋與查證主題...");
            var queryPlan = BuildSearchQueryPlan(request);

            var sources = await VerifySourcesAsync(queryPlan, cancellationToken);

            Report(ProgressStage.SynthesizingItinerary, "正在整合已驗證資訊並生成行程...");
            var plan = await ComposePlanAsync(request, sources, cancellationToken);

            Report(ProgressStage.FormattingChecklist, "正在整理旅遊行程建議清單...");
            var output = _composer.Compose(plan, sources);

            Report(ProgressStage.Completed, "旅遊行程建議清單已整理完成。");
            return output;
        }
        catch
        {
            Report(ProgressStage.Failed, "行程生成失敗，正在整理錯誤原因...");
            throw;
        }
    }

    public SearchQueryPlan BuildSearchQueryPlan(TravelRequest request)
    {
        var basis = $"{request.Destination} {request.TravelStyle}".Trim();
        var queries = new List<string>
        {
            $"{basis} 在地美食 最新資訊",
            $"{request.Destination} 交通 官方 最新資訊",
            $"{request.Destination} 住宿 區域 官方 最新資訊"
        };

        if (request.SpecialRequirements.Any(item => item.Contains("蛋塔", StringComparison.OrdinalIgnoreCase)))
        {
            queries.Add($"{request.Destination} 蛋塔 推薦 分店 最新資訊");
        }

        return new SearchQueryPlan { Queries = queries };
    }

    private async Task<TravelRequest> ParseRequestAsync(string conversation, CancellationToken cancellationToken)
    {
        return await _llm.CompleteJsonAsync<TravelRequest>(
        [
            new LlmMessage("system", """
                你是旅遊需求分析助手。請把使用者輸入整理成 JSON，欄位固定如下：
                destination, days, travelStyle, transportationPreference, budget, specialRequirements

                請遵守以下規則：
                1. 只能輸出 JSON，不要輸出 markdown 或其他說明文字。
                2. specialRequirements 必須是字串陣列，例如 ["想吃在地早餐", "需要外帶蛋塔回飯店"]。
                3. days 必須是整數。
                4. 如果使用者描述了特殊偏好、限制或購買需求，請整理到 specialRequirements。
                """),
            new LlmMessage("user", conversation)
        ], cancellationToken);
    }

    private async Task<List<VerifiedSource>> VerifySourcesAsync(SearchQueryPlan queryPlan, CancellationToken cancellationToken)
    {
        var sources = new List<VerifiedSource>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var query in queryPlan.Queries)
        {
            Report(ProgressStage.SearchingWeb, $"正在搜尋：{query}");
            var candidates = await _searchService.SearchAsync(query, cancellationToken);
            Report(ProgressStage.VerifyingSources, $"正在驗證 {candidates.Count} 筆候選來源...");

            foreach (var candidate in candidates)
            {
                if (!seen.Add(candidate.Url))
                {
                    continue;
                }

                try
                {
                    sources.Add(await _pageVerifier.VerifyAsync(candidate.Url, cancellationToken));
                }
                catch
                {
                    // Keep the sample simple: ignore individual source failures.
                }
            }
        }

        return sources;
    }

    private async Task<TravelPlan> ComposePlanAsync(
        TravelRequest request,
        IReadOnlyCollection<VerifiedSource> sources,
        CancellationToken cancellationToken)
    {
        var verifiedFacts = sources.SelectMany(source => source.Facts.Select(fact => new VerifiedFact
        {
            Category = GuessCategory(fact),
            Subject = source.Title,
            Detail = fact,
            SourceUrl = source.Url,
            SourceTitle = source.Title
        })).ToList();

        var payload = new
        {
            request,
            verifiedFacts
        };

        return await _llm.CompleteJsonAsync<TravelPlan>(
        [
            new LlmMessage("system", """
                你是旅遊行程規劃助手。請只根據 request 和 verifiedFacts 產生旅遊行程 JSON。
                不可以捏造未出現在 verifiedFacts 的景點、餐廳、交通或住宿資訊。

                請遵守以下規則：
                1. 只能輸出 JSON，不要輸出 markdown 或其他說明。
                2. transportationNotes、accommodationNotes、cautions 必須是字串陣列。
                3. dailyPlans 必須是陣列，每個項目都要有 day、theme、items。
                4. items 必須是陣列，每個項目都要有 category、name、description。
                5. JSON 欄位固定如下：
                   summary,
                   dailyPlans[{day,theme,items[{category,name,description}]}],
                   transportationNotes,
                   accommodationNotes,
                   cautions
                """),
            new LlmMessage("user", JsonSerializer.Serialize(payload, TravelRequestSerializer.Options))
        ], cancellationToken);
    }

    private static string GuessCategory(string fact)
    {
        if (fact.Contains("住宿", StringComparison.OrdinalIgnoreCase))
        {
            return "住宿";
        }

        if (fact.Contains("交通", StringComparison.OrdinalIgnoreCase) || fact.Contains("車站", StringComparison.OrdinalIgnoreCase))
        {
            return "交通";
        }

        return "景點";
    }

    private PlanningMode ChoosePlanningMode(TravelRequest request, string conversation)
    {
        if (string.IsNullOrWhiteSpace(request.Destination) || request.Days <= 0)
        {
            return PlanningMode.Clarify;
        }

        var complexitySignals = 0;

        if (conversation.Length >= 80)
        {
            complexitySignals++;
        }

        if (request.SpecialRequirements.Count >= 3)
        {
            complexitySignals++;
        }

        if (request.SpecialRequirements.Any(item =>
                item.Contains("另外", StringComparison.OrdinalIgnoreCase) ||
                item.Contains("還需要", StringComparison.OrdinalIgnoreCase) ||
                item.Contains("比較", StringComparison.OrdinalIgnoreCase) ||
                item.Contains("推薦理由", StringComparison.OrdinalIgnoreCase)))
        {
            complexitySignals++;
        }

        return complexitySignals >= 2 ? PlanningMode.Iterative : PlanningMode.Direct;
    }

    private void ReportStrategy(PlanningMode mode)
    {
        switch (mode)
        {
            case PlanningMode.Direct:
                Report(ProgressStage.ChoosingStrategy, "這次需求較完整，將直接規劃行程...");
                break;
            case PlanningMode.Iterative:
                Report(ProgressStage.ChoosingStrategy, "這次需求較複雜，將先分步整理候選建議...");
                break;
            case PlanningMode.Clarify:
                Report(ProgressStage.ChoosingStrategy, "正在評估缺少的關鍵資訊...");
                Report(ProgressStage.ClarifyingMissingInfo, "目前缺少部分關鍵資訊，先用已知條件整理可行建議...");
                break;
        }
    }

    private void Report(
        ProgressStage stage,
        string message,
        ProgressDetailLevel detailLevel = ProgressDetailLevel.Normal)
    {
        _progressReporter.Report(new ProgressUpdate(stage, message, detailLevel));
    }
}

internal enum PlanningMode
{
    Direct,
    Iterative,
    Clarify
}
