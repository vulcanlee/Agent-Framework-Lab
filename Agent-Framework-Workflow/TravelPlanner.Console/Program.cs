using System.ClientModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAIChatClient = OpenAI.Chat.ChatClient;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;

try
{
	DemoOptions options = DemoOptions.FromEnvironment();
	OpenAIChatClient rawChatClient = CreateGitHubModelsChatClient(options);
	IChatClient baseChatClient = rawChatClient.AsIChatClient();

	if (args.Length > 0)
	{
		string travelRequest = string.Join(' ', args).Trim();
		await RunScenarioAsync(baseChatClient, options, travelRequest, WorkflowSelection.Both);
	}
	else
	{
		await RunReplAsync(baseChatClient, options);
	}
}
catch (Exception ex)
{
	Console.ForegroundColor = ConsoleColor.Red;
	Console.Error.WriteLine($"執行失敗: {ex.Message}");
	Console.ResetColor();
	Environment.ExitCode = 1;
}

OpenAIChatClient CreateGitHubModelsChatClient(DemoOptions options)
{
#pragma warning disable OPENAI001
	return new OpenAIChatClient(
		options.ModelId,
		new ApiKeyCredential(options.GitHubToken),
		new OpenAIClientOptions
		{
			Endpoint = new Uri(options.Endpoint)
		});
#pragma warning restore OPENAI001
}

async Task<string> RunAgentDemoAsync(IChatClient baseChatClient, DemoOptions options, string request)
{
	List<StageTiming> stages = [];
	Stopwatch totalStopwatch = Stopwatch.StartNew();

	List<AITool> tools =
	[
		AIFunctionFactory.Create(GetTransitAdvice, name: "get_transit_advice", description: "提供週末旅遊的交通建議。"),
		AIFunctionFactory.Create(GetPackingAdvice, name: "get_packing_advice", description: "依據目的地與旅遊風格提供打包建議。"),
		AIFunctionFactory.Create(GetBudgetTip, name: "get_budget_tip", description: "依據預算提供控制花費的提醒。")
	];

	IChatClient toolEnabledClient = await ExecuteStageAsync(
		"Agent",
		"建立可呼叫工具的 ChatClient",
		stages,
		() => Task.FromResult(
			new ChatClientBuilder(baseChatClient)
				.UseFunctionInvocation()
				.Build()));

	ChatClientAgent agent = await ExecuteStageAsync(
		"Agent",
		"建立旅遊規劃 Agent",
		stages,
		() => Task.FromResult(
			toolEnabledClient.AsAIAgent(
				instructions:
					"""
					你是「週末旅遊行程規劃助手」。
					你的任務是把使用者的需求整理成容易執行的兩天一夜或當日行程建議。
					回覆時請遵守：
					1. 先用 2 到 3 行摘要旅遊策略。
					2. 再給出 Day 1 / Day 2 或上午 / 下午 / 晚上安排。
					3. 若預算或條件不完整，主動補上合理假設並明講。
					4. 有需要時再呼叫工具，不要為了展示而硬用工具。
					5. 用繁體中文，保持清楚、實用、口語。
					""",
				name: "WeekendTravelPlanner",
				description: "用 Agent 方式示範生活化旅遊規劃",
				tools: tools)));

	AgentSession session = await ExecuteStageAsync(
		"Agent",
		"建立 Agent Session",
		stages,
		async () => await agent.CreateSessionAsync());

	AgentResponse response = await ExecuteStageAsync(
		"Agent",
		"執行 Agent 規劃",
		stages,
		() => agent.RunAsync(
			request,
			session,
			new ChatClientAgentRunOptions(
				new ChatOptions
				{
					ModelId = options.ModelId
				})));

	totalStopwatch.Stop();

	return BuildExecutionResult(
		response.Messages.LastOrDefault()?.Text?.Trim()
			?? response.ToString()
			?? "Agent 沒有產生內容。",
		stages,
		totalStopwatch.Elapsed);
}

async Task<string> RunWorkflowDemoAsync(IChatClient baseChatClient, DemoOptions options, string request)
{
	List<StageTiming> stages = [];
	Stopwatch totalStopwatch = Stopwatch.StartNew();

	var normalize = new FunctionExecutor<string, TripPlanState>(
		"NormalizeRequest",
		async (input, _, _) =>
			await ExecuteStageValueAsync(
				"Workflow",
				"需求正規化",
				stages,
				() => ValueTask.FromResult(ParseRequest(input))));

	var budgetGuard = new FunctionExecutor<TripPlanState, TripPlanState>(
		"BudgetGuard",
		async (state, _, _) =>
			await ExecuteStageValueAsync(
				"Workflow",
				"預算檢查",
				stages,
				() => ValueTask.FromResult(ApplyBudgetGuardrails(state))));

	var attractions = new FunctionExecutor<TripPlanState, TripPlanState>(
		"PlanAttractions",
		(state, _, cancellationToken) =>
			ExecuteStageValueAsync(
				"Workflow",
				"景點規劃",
				stages,
				async () =>
				{
					string prompt =
						$"""
						你是一位旅遊編輯，請為以下條件規劃景點安排。
						目的地: {state.Destination}
						天數: {state.Days}
						同行人數: {state.Travelers}
						旅遊風格: {state.Style}
						預算: 每人約 {state.BudgetPerPerson} TWD
						限制: {state.Constraints}

						請輸出 3 到 5 條景點安排建議，每條都包含時間帶與理由。
						請避免 JSON，直接輸出條列文字。
						""";

					ChatResponse response = await baseChatClient.GetResponseAsync(
						[new ChatMessage(ChatRole.User, prompt)],
						new ChatOptions { ModelId = options.ModelId },
						cancellationToken);

					return state with { AttractionPlan = response.Text.Trim() };
				}));

	var meals = new FunctionExecutor<TripPlanState, TripPlanState>(
		"PlanMeals",
		(state, _, cancellationToken) =>
			ExecuteStageValueAsync(
				"Workflow",
				"餐食規劃",
				stages,
				async () =>
				{
					string prompt =
						$"""
						根據這份週末旅遊草案，補上餐食建議。
						目的地: {state.Destination}
						旅遊風格: {state.Style}
						預算: 每人約 {state.BudgetPerPerson} TWD
						已有景點安排:
						{state.AttractionPlan}

						請提供早餐、午餐、晚餐或下午茶建議，讓整體路線合理。
						請直接輸出條列文字。
						""";

					ChatResponse response = await baseChatClient.GetResponseAsync(
						[new ChatMessage(ChatRole.User, prompt)],
						new ChatOptions { ModelId = options.ModelId },
						cancellationToken);

					return state with { MealPlan = response.Text.Trim() };
				}));

	var compose = new FunctionExecutor<TripPlanState, string>(
		"ComposeItinerary",
		async (state, _, _) =>
			await ExecuteStageValueAsync(
				"Workflow",
				"組裝最終輸出",
				stages,
				() => ValueTask.FromResult(ComposeWorkflowResult(state))));

	Workflow workflow = new WorkflowBuilder(normalize)
		.AddEdge(normalize, budgetGuard)
		.AddEdge(budgetGuard, attractions)
		.AddEdge(attractions, meals)
		.AddEdge(meals, compose)
		.WithOutputFrom(compose)
		.Build();

	await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, request);

	string? finalOutput = null;

	await foreach (WorkflowEvent evt in run.WatchStreamAsync())
	{
		switch (evt)
		{
			case WorkflowOutputEvent outputEvent when outputEvent.Data is string text:
				finalOutput = text;
				break;
			case WorkflowErrorEvent workflowError:
				throw workflowError.Exception ?? new InvalidOperationException("Workflow 發生未提供詳細資訊的錯誤。");
			case ExecutorFailedEvent executorFailed:
				throw new InvalidOperationException($"Workflow executor '{executorFailed.ExecutorId}' 執行失敗。");
		}
	}

	totalStopwatch.Stop();

	return BuildExecutionResult(
		finalOutput ?? "Workflow 沒有產生內容。",
		stages,
		totalStopwatch.Elapsed);
}

TripPlanState ParseRequest(string input)
{
	string destination = ExtractDestination(input) ?? "台北近郊";
	int days = ExtractDays(input) ?? 2;
	int travelers = ExtractTravelers(input) ?? 2;
	decimal budget = ExtractBudget(input) ?? 3000m;
	string style = ExtractStyle(input);
	string constraints = ExtractConstraints(input);

	return new TripPlanState(
		OriginalRequest: input,
		Destination: destination,
		Days: days,
		Travelers: travelers,
		BudgetPerPerson: budget,
		Style: style,
		Constraints: constraints,
		BudgetAssessment: string.Empty,
		AttractionPlan: string.Empty,
		MealPlan: string.Empty,
		Warnings: []);
}

TripPlanState ApplyBudgetGuardrails(TripPlanState state)
{
	List<string> warnings = [.. state.Warnings];
	string assessment;

	if (state.BudgetPerPerson < 1500m)
	{
		assessment = "預算偏緊，建議以大眾運輸、平價小吃與免費景點為主。";
		warnings.Add("若想加入熱門餐廳或付費展館，可能需要提高預算。");
	}
	else if (state.BudgetPerPerson < 3500m)
	{
		assessment = "預算中等，可安排 1 到 2 個付費景點與 1 餐特色餐廳。";
	}
	else
	{
		assessment = "預算充足，可以安排較完整的交通、餐飲與特色體驗。";
	}

	if (state.Days <= 1)
	{
		warnings.Add("需求偏向單日行程，晚上安排應盡量集中，避免拉車過久。");
	}

	return state with
	{
		BudgetAssessment = assessment,
		Warnings = warnings
	};
}

string ComposeWorkflowResult(TripPlanState state)
{
	StringBuilder builder = new();
	builder.AppendLine("這是用 Workflow 拆解後得到的行程草案。");
	builder.AppendLine();
	builder.AppendLine("需求正規化");
	builder.AppendLine($"- 目的地: {state.Destination}");
	builder.AppendLine($"- 天數: {state.Days}");
	builder.AppendLine($"- 同行人數: {state.Travelers}");
	builder.AppendLine($"- 旅遊風格: {state.Style}");
	builder.AppendLine($"- 每人預算: {state.BudgetPerPerson.ToString("0", CultureInfo.InvariantCulture)} TWD");
	builder.AppendLine($"- 限制條件: {state.Constraints}");
	builder.AppendLine();
	builder.AppendLine("預算檢查");
	builder.AppendLine(state.BudgetAssessment);

	if (state.Warnings.Count > 0)
	{
		builder.AppendLine();
		builder.AppendLine("提醒");
		foreach (string warning in state.Warnings)
		{
			builder.AppendLine($"- {warning}");
		}
	}

	builder.AppendLine();
	builder.AppendLine("景點安排");
	builder.AppendLine(state.AttractionPlan);
	builder.AppendLine();
	builder.AppendLine("餐食安排");
	builder.AppendLine(state.MealPlan);

	return builder.ToString().Trim();
}

async Task RunReplAsync(IChatClient baseChatClient, DemoOptions options)
{
	WorkflowSelection currentSelection = WorkflowSelection.Both;

	WriteReplWelcome(options, currentSelection);

	while (true)
	{
		Console.ForegroundColor = ConsoleColor.DarkGray;
		Console.Write($"[{SelectionLabel(currentSelection)}] > ");
		Console.ResetColor();

		string? input = Console.ReadLine()?.Trim();

		if (string.IsNullOrWhiteSpace(input))
		{
			await RunScenarioAsync(baseChatClient, options, GetDefaultTravelRequest(), currentSelection);
			continue;
		}

		if (input.Equals("/exit", StringComparison.OrdinalIgnoreCase) || input.Equals("/quit", StringComparison.OrdinalIgnoreCase))
		{
			Console.WriteLine("結束 REPL。下次再見。");
			return;
		}

		if (input.Equals("/help", StringComparison.OrdinalIgnoreCase))
		{
			WriteReplHelp(currentSelection);
			continue;
		}

		if (TryParseModeCommand(input, out WorkflowSelection nextSelection))
		{
			currentSelection = nextSelection;
			Console.WriteLine($"已切換執行模式為: {SelectionLabel(currentSelection)}");
			continue;
		}

		await RunScenarioAsync(baseChatClient, options, input, currentSelection);
	}
}

void WriteHeader(DemoOptions options, string request)
{
	Console.WriteLine("Microsoft Agent Framework 入門示範");
	Console.WriteLine("主題: 週末旅遊行程規劃助手");
	Console.WriteLine($"模型: {options.ModelId}");
	Console.WriteLine($"端點: {options.Endpoint}");
	Console.WriteLine();
	Console.WriteLine("使用者需求");
	Console.WriteLine(request);
	Console.WriteLine();
}

void WriteReplWelcome(DemoOptions options, WorkflowSelection currentSelection)
{
	Console.WriteLine("Microsoft Agent Framework 入門示範");
	Console.WriteLine("模式: REPL 互動式旅遊規劃工作台");
	Console.WriteLine($"模型: {options.ModelId}");
	Console.WriteLine($"端點: {options.Endpoint}");
	Console.WriteLine($"目前執行模式: {SelectionLabel(currentSelection)}");
	Console.WriteLine();
	Console.WriteLine("輸入旅遊需求後按 Enter 即可執行。直接按 Enter 會使用預設示範。輸入 /help 可查看指令。\n");
}

void WriteReplHelp(WorkflowSelection currentSelection)
{
	Console.WriteLine("可用指令");
	Console.WriteLine("/help             顯示說明");
	Console.WriteLine("/mode agent       只跑 Agent");
	Console.WriteLine("/mode workflow    只跑 Workflow");
	Console.WriteLine("/mode both        先跑 Agent，再跑 Workflow");
	Console.WriteLine("/exit 或 /quit    結束 REPL");
	Console.WriteLine();
	Console.WriteLine($"目前執行模式: {SelectionLabel(currentSelection)}");
	Console.WriteLine();
}

bool TryParseModeCommand(string input, out WorkflowSelection selection)
{
	selection = WorkflowSelection.Both;

	string normalized = input.Trim();
	if (!normalized.StartsWith("/mode", StringComparison.OrdinalIgnoreCase))
	{
		return false;
	}

	string[] parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
	if (parts.Length != 2)
	{
		Console.WriteLine("模式指令格式為 /mode agent|workflow|both");
		return true;
	}

	selection = parts[1].ToLowerInvariant() switch
	{
		"agent" => WorkflowSelection.Agent,
		"workflow" => WorkflowSelection.Workflow,
		"both" => WorkflowSelection.Both,
		_ => throw new InvalidOperationException("不支援的模式。請使用 agent、workflow 或 both。")
	};

	return true;
}

async Task RunScenarioAsync(IChatClient baseChatClient, DemoOptions options, string travelRequest, WorkflowSelection selection)
{
	Stopwatch totalStopwatch = Stopwatch.StartNew();

	Console.WriteLine();
	WriteHeader(options, travelRequest);
	Console.WriteLine($"本次執行模式: {SelectionLabel(selection)}");
	Console.WriteLine();

	if (selection is WorkflowSelection.Agent or WorkflowSelection.Both)
	{
		WriteWorkflowBoundary("Agent", isEntering: true);
		string agentResult = await RunAgentDemoAsync(baseChatClient, options, travelRequest);
		WriteWorkflowBoundary("Agent", isEntering: false);
		WriteSection("Agent 結果", agentResult);
	}

	if (selection is WorkflowSelection.Workflow or WorkflowSelection.Both)
	{
		WriteWorkflowBoundary("Workflow", isEntering: true);
		string workflowResult = await RunWorkflowDemoAsync(baseChatClient, options, travelRequest);
		WriteWorkflowBoundary("Workflow", isEntering: false);
		WriteSection("Workflow 結果", workflowResult);
	}

	totalStopwatch.Stop();

	WriteSection(
		"如何閱讀這個範例",
		"Agent 版本把較多判斷交給模型，並可自主決定是否呼叫工具。\n" +
		"Workflow 版本則把流程拆成明確步驟：需求正規化、預算檢查、景點規劃、餐食規劃、最後組裝輸出。\n" +
		"同一個情境下，前者偏彈性，後者偏可預測與可治理。"
	);

	Console.ForegroundColor = ConsoleColor.Green;
	Console.WriteLine($"本次總耗時: {FormatElapsed(totalStopwatch.Elapsed)}");
	Console.ResetColor();
	Console.WriteLine();
}

void WriteWorkflowBoundary(string workflowName, bool isEntering)
{
	Console.ForegroundColor = isEntering ? ConsoleColor.Cyan : ConsoleColor.DarkCyan;
	Console.WriteLine(isEntering
		? $"[開始] 進入 {workflowName} 流程"
		: $"[完成] 離開 {workflowName} 流程");
	Console.ResetColor();
}

async Task<T> ExecuteStageAsync<T>(string workflowName, string stageName, List<StageTiming> stages, Func<Task<T>> action)
{
	WriteStageStatus(workflowName, stageName, isStarting: true, elapsed: null);
	Stopwatch stopwatch = Stopwatch.StartNew();

	try
	{
		T result = await action();
		stopwatch.Stop();
		stages.Add(new StageTiming(stageName, stopwatch.Elapsed));
		WriteStageStatus(workflowName, stageName, isStarting: false, elapsed: stopwatch.Elapsed);
		return result;
	}
	catch
	{
		stopwatch.Stop();
		WriteStageFailure(workflowName, stageName, stopwatch.Elapsed);
		throw;
	}
}

async ValueTask<T> ExecuteStageValueAsync<T>(string workflowName, string stageName, List<StageTiming> stages, Func<ValueTask<T>> action)
{
	WriteStageStatus(workflowName, stageName, isStarting: true, elapsed: null);
	Stopwatch stopwatch = Stopwatch.StartNew();

	try
	{
		T result = await action();
		stopwatch.Stop();
		stages.Add(new StageTiming(stageName, stopwatch.Elapsed));
		WriteStageStatus(workflowName, stageName, isStarting: false, elapsed: stopwatch.Elapsed);
		return result;
	}
	catch
	{
		stopwatch.Stop();
		WriteStageFailure(workflowName, stageName, stopwatch.Elapsed);
		throw;
	}
}

void WriteStageStatus(string workflowName, string stageName, bool isStarting, TimeSpan? elapsed)
{
	Console.ForegroundColor = isStarting ? ConsoleColor.Yellow : ConsoleColor.DarkYellow;
	Console.WriteLine(isStarting
		? $"[{workflowName}] 開始階段: {stageName}"
		: $"[{workflowName}] 完成階段: {stageName}，耗時 {FormatElapsed(elapsed!.Value)}");
	Console.ResetColor();
}

void WriteStageFailure(string workflowName, string stageName, TimeSpan elapsed)
{
	Console.ForegroundColor = ConsoleColor.Red;
	Console.WriteLine($"[{workflowName}] 階段失敗: {stageName}，已耗時 {FormatElapsed(elapsed)}");
	Console.ResetColor();
}

string BuildExecutionResult(string output, IReadOnlyList<StageTiming> stages, TimeSpan totalElapsed)
{
	StringBuilder builder = new();
	builder.AppendLine(output.Trim());
	builder.AppendLine();
	builder.AppendLine("階段耗時");

	foreach (StageTiming stage in stages)
	{
		builder.AppendLine($"- {stage.Name}: {FormatElapsed(stage.Elapsed)}");
	}

	builder.AppendLine($"- 總計: {FormatElapsed(totalElapsed)}");
	return builder.ToString().Trim();
}

string SelectionLabel(WorkflowSelection selection) =>
	selection switch
	{
		WorkflowSelection.Agent => "Agent",
		WorkflowSelection.Workflow => "Workflow",
		_ => "Agent + Workflow"
	};

string FormatElapsed(TimeSpan elapsed)
{
	if (elapsed.TotalSeconds >= 1)
	{
		return $"{elapsed.TotalSeconds:0.00} 秒";
	}

	return $"{elapsed.TotalMilliseconds:0} ms";
}

string GetDefaultTravelRequest() =>
	"幫我規劃兩天一夜台中週末旅行，兩個大人同行，每人預算 3500 元，希望行程輕鬆、有咖啡店，也想吃在地美食。";

void WriteSection(string title, string content)
{
	Console.WriteLine(new string('=', 72));
	Console.WriteLine(title);
	Console.WriteLine(new string('=', 72));
	Console.WriteLine(content);
	Console.WriteLine();
}

string? ExtractDestination(string input)
{
	Match destinationMatch = Regex.Match(
		input,
		"(台北|新北|基隆|桃園|新竹|苗栗|台中|彰化|南投|雲林|嘉義|台南|高雄|屏東|宜蘭|花蓮|台東|澎湖|金門|馬祖|東京|大阪|京都|福岡|首爾|釜山|香港|沖繩)",
		RegexOptions.IgnoreCase);

	return destinationMatch.Success ? destinationMatch.Value : null;
}

int? ExtractDays(string input)
{
	Match numericDays = Regex.Match(input, @"(?<days>\d+)\s*(天|日)");
	if (numericDays.Success && int.TryParse(numericDays.Groups["days"].Value, out int parsedDays))
	{
		return parsedDays;
	}

	if (input.Contains("兩天一夜", StringComparison.Ordinal))
	{
		return 2;
	}

	if (input.Contains("一日", StringComparison.Ordinal) || input.Contains("當天來回", StringComparison.Ordinal))
	{
		return 1;
	}

	return null;
}

int? ExtractTravelers(string input)
{
	Match travelerMatch = Regex.Match(input, @"(?<count>\d+)\s*(人|位)");
	if (travelerMatch.Success && int.TryParse(travelerMatch.Groups["count"].Value, out int travelers))
	{
		return travelers;
	}

	if (input.Contains("兩個大人", StringComparison.Ordinal) || input.Contains("兩人", StringComparison.Ordinal))
	{
		return 2;
	}

	if (input.Contains("家庭", StringComparison.Ordinal))
	{
		return 4;
	}

	return null;
}

decimal? ExtractBudget(string input)
{
	Match budgetMatch = Regex.Match(input, @"(?<budget>\d{3,5})\s*(元|塊|TWD)", RegexOptions.IgnoreCase);
	if (budgetMatch.Success && decimal.TryParse(budgetMatch.Groups["budget"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal budget))
	{
		return budget;
	}

	return null;
}

string ExtractStyle(string input)
{
	if (input.Contains("輕鬆", StringComparison.Ordinal) || input.Contains("慢遊", StringComparison.Ordinal))
	{
		return "輕鬆慢遊";
	}

	if (input.Contains("美食", StringComparison.Ordinal))
	{
		return "美食探索";
	}

	if (input.Contains("拍照", StringComparison.Ordinal) || input.Contains("打卡", StringComparison.Ordinal))
	{
		return "拍照打卡";
	}

	return "生活感散步";
}

string ExtractConstraints(string input)
{
	List<string> tags = [];

	if (input.Contains("咖啡", StringComparison.Ordinal))
	{
		tags.Add("想安排咖啡店");
	}

	if (input.Contains("在地美食", StringComparison.Ordinal) || input.Contains("美食", StringComparison.Ordinal))
	{
		tags.Add("想吃在地美食");
	}

	if (input.Contains("親子", StringComparison.Ordinal))
	{
		tags.Add("需要親子友善");
	}

	if (input.Contains("不開車", StringComparison.Ordinal) || input.Contains("大眾運輸", StringComparison.Ordinal))
	{
		tags.Add("以大眾運輸為主");
	}

	return tags.Count == 0 ? "未提供特殊限制，採用一般週末旅行假設。" : string.Join("、", tags);
}

[Description("提供交通安排建議")]
string GetTransitAdvice(
	[Description("旅遊目的地，例如台中或京都")] string destination,
	[Description("旅遊天數")] int days) =>
	destination switch
	{
		"台中" => days > 1 ? "建議高鐵搭配市區公車與步行，第二天可集中在審計新村與草悟道周邊。" : "建議高鐵加計程車短程接駁，避免單日拉車太久。",
		"台北" or "新北" => "捷運加步行最穩，若跨區可補 YouBike 或計程車。",
		"高雄" => "以捷運與輕軌串聯港區景點最有效率。",
		_ => "先確認主要移動半徑，再決定是否用大眾運輸或租車，週末行程以少換乘為原則。"
	};

[Description("提供打包建議")]
string GetPackingAdvice(
	[Description("旅遊目的地")] string destination,
	[Description("旅遊風格，例如輕鬆慢遊或美食探索")] string style) =>
	$"前往 {destination} 的 {style} 行程，建議準備輕便雨具、水壺、行動電源，以及一雙適合久走的鞋。";

[Description("提供預算提醒")]
string GetBudgetTip(
	[Description("每人預算，單位 TWD")] decimal budgetPerPerson) =>
	budgetPerPerson switch
	{
		< 1500m => "預算較低，建議把大部分費用保留給交通與主餐，甜點與伴手禮保持彈性。",
		< 3500m => "預算中等，可安排一餐重點餐廳，但其餘時段建議選擇高 CP 值選項。",
		_ => "預算相對充足，可保留 15% 作為臨時加點、展館門票或計程車緩衝。"
	};

sealed record DemoOptions(string Endpoint, string GitHubToken, string ModelId)
{
	public static DemoOptions FromEnvironment()
	{
		string? token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
		if (string.IsNullOrWhiteSpace(token))
		{
			throw new InvalidOperationException("找不到 GITHUB_TOKEN。請先設定 GitHub Models API token。");
		}

		string endpoint = Environment.GetEnvironmentVariable("GITHUB_MODELS_ENDPOINT")
			?? "https://models.github.ai/inference";

		string modelId = Environment.GetEnvironmentVariable("GITHUB_MODEL")
			?? "openai/gpt-4.1-mini";

		return new DemoOptions(endpoint, token, modelId);
	}
}

sealed record TripPlanState(
	string OriginalRequest,
	string Destination,
	int Days,
	int Travelers,
	decimal BudgetPerPerson,
	string Style,
	string Constraints,
	string BudgetAssessment,
	string AttractionPlan,
	string MealPlan,
	IReadOnlyList<string> Warnings);

sealed record StageTiming(string Name, TimeSpan Elapsed);

enum WorkflowSelection
{
	Agent,
	Workflow,
	Both
}
