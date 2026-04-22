using NvidiaTravelAgent.Models;
using System.Text;
using System.Text.Json;

namespace NvidiaTravelAgent.Services;

public sealed class TravelPlannerEngine
{
    private readonly INvidiaChatClient _llm;
    private readonly IWebSearchService _searchService;
    private readonly IWebPageVerifier _pageVerifier;
    private readonly ItineraryComposer _composer;

    public TravelPlannerEngine(INvidiaChatClient llm, IWebSearchService searchService, IWebPageVerifier pageVerifier, ItineraryComposer composer)
    {
        _llm = llm;
        _searchService = searchService;
        _pageVerifier = pageVerifier;
        _composer = composer;
    }

    public async Task<string> PlanAsync(string conversation, CancellationToken cancellationToken = default)
    {
        var request = await ParseRequestAsync(conversation, cancellationToken);
        var queryPlan = BuildSearchQueryPlan(request);
        var sources = await VerifySourcesAsync(queryPlan, cancellationToken);
        var plan = await ComposePlanAsync(request, sources, cancellationToken);
        return _composer.Compose(plan, sources);
    }

    public SearchQueryPlan BuildSearchQueryPlan(TravelRequest request)
    {
        var basis = $"{request.Destination} {request.TravelStyle}".Trim();
        return new SearchQueryPlan
        {
            Queries =
            [
                $"{basis} 景點 官方",
                $"{request.Destination} 大眾運輸 官方",
                $"{request.Destination} 住宿 官方"
            ]
        };
    }

    private async Task<TravelRequest> ParseRequestAsync(string conversation, CancellationToken cancellationToken)
    {
        return await _llm.CompleteJsonAsync<TravelRequest>(
        [
            new LlmMessage("system", """
                你是旅遊需求解析器。請將使用者需求轉為 JSON，欄位固定為:
                destination, days, travelStyle, transportationPreference, budget, specialRequirements。
                僅輸出 JSON，不要加說明文字。
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
            var candidates = await _searchService.SearchAsync(query, cancellationToken);
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
                    // Ignore individual source failures to keep the sample straightforward.
                }
            }
        }

        return sources;
    }

    private async Task<TravelPlan> ComposePlanAsync(TravelRequest request, IReadOnlyCollection<VerifiedSource> sources, CancellationToken cancellationToken)
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
                你是旅遊行程規劃器。只能根據提供的 verifiedFacts 產生旅遊行程 JSON。
                不可虛構景點、交通或住宿資訊。
                欄位固定為:
                summary, dailyPlans[{day,theme,items[{category,name,description}]}], transportationNotes, accommodationNotes, cautions。
                僅輸出 JSON。
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

        if (fact.Contains("交通", StringComparison.OrdinalIgnoreCase) || fact.Contains("班次", StringComparison.OrdinalIgnoreCase))
        {
            return "交通";
        }

        return "景點";
    }
}
