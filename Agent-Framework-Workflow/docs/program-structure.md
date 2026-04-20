# Program.cs 導讀

## 摘要

這個專案把 Microsoft Agent Framework 的兩種典型寫法，放進同一支主控台程式裡。

你可以把 [TravelPlanner.Console/Program.cs](../TravelPlanner.Console/Program.cs) 想成一個小型控制台：

- 先讀取環境變數與模型設定
- 建立 GitHub Models 用的 `ChatClient`
- 視執行方式走單次執行或 REPL
- 在單次請求中選擇跑 Agent、Workflow，或兩者都跑
- 把進度、階段耗時、最後結果印到畫面上

如果你完全不熟 Agent Framework，這份文件的目標不是讓你一次記住所有 API，而是先建立一個穩定的理解順序：

1. 先看程式入口怎麼把整個範例啟動起來。
2. 再看 `RunScenarioAsync()` 怎麼協調 Agent 與 Workflow。
3. 接著分開看 `RunAgentDemoAsync()` 與 `RunWorkflowDemoAsync()`。
4. 最後才看各種輔助函式與之後最常修改的地方。

照這個順序讀，你比較不會在一開始就卡在細節裡。

## 怎麼讀這支程式

建議用下面這個順序理解：

### 第一層：入口與分流

先看最上面的 top-level 程式碼。

這一段做了四件事：

1. 設定 `Console.InputEncoding` 與 `Console.OutputEncoding` 為 UTF-8，確保繁體中文輸出正常。
2. 透過 `DemoOptions.FromEnvironment()` 讀取模型設定。
3. 透過 `CreateGitHubModelsChatClient()` 建立和 GitHub Models 溝通的 `OpenAIChatClient`。
4. 根據 `args.Length` 決定要走單次執行，還是進入 REPL 模式。

這一層的重點不是 AI，而是「把程式啟動起來」。

### 第二層：單次請求的協調中心

接著看 `RunScenarioAsync()`。

這個函式很重要，因為它不是在做模型推理，而是在做協調：

- 印出標題、模型、端點、使用者需求
- 判斷本次要跑 `Agent`、`Workflow` 或 `Both`
- 在進入與離開流程前後印出提示訊息
- 收集整次請求總耗時
- 在最後補上這個範例的閱讀提示

如果你日後要新增新的執行模式，例如只跑某個 Workflow 子流程，通常會先改這裡。

### 第三層：兩條核心路線

再往下看：

- `RunAgentDemoAsync()`
- `RunWorkflowDemoAsync()`

這兩個函式是整個專案最值得讀的部分。

它們示範的是同一個旅遊需求，如何用兩種不同思路寫出來：

- Agent 路線：把較多判斷交給模型
- Workflow 路線：把流程切成明確步驟，由程式決定順序

### 第四層：支援這兩條路線的普通 C# 函式

最後才看：

- `ParseRequest()`、`ExtractDestination()`、`ExtractBudget()` 這類解析函式
- `ApplyBudgetGuardrails()` 這類規則函式
- `GetTransitAdvice()`、`GetPackingAdvice()`、`GetBudgetTip()` 這類工具函式
- `WriteSection()`、`FormatElapsed()` 這類輸出輔助函式

這一層很重要，因為它提醒你一件事：

不是所有事情都要交給 Agent 或 Workflow。很多地方其實只是一般 C# 邏輯。

## 程式入口在做什麼

### UTF-8 設定

最前面先設定主控台輸入輸出編碼：

- `Console.InputEncoding = Encoding.UTF8;`
- `Console.OutputEncoding = Encoding.UTF8;`

這是因為整個範例都使用繁體中文。如果少了這一段，Windows 主控台很容易出現亂碼。

### 最外層 try/catch

接著用一個最外層 `try/catch` 包住整個啟動流程。

這代表：

- 缺少 `GITHUB_TOKEN` 時，不會讓使用者看到難懂的例外堆疊
- 建立模型連線失敗時，可以統一用紅字顯示「執行失敗」
- 程式也會設定 `Environment.ExitCode = 1`，方便外部工具辨識失敗

這不是 Agent Framework 特有寫法，而是一般命令列程式應該有的基本防線。

### DemoOptions.FromEnvironment()

`DemoOptions` 是一個很小的設定物件，專門管理三個資訊：

- `Endpoint`
- `GitHubToken`
- `ModelId`

`FromEnvironment()` 的作用是把環境變數讀成這個物件：

- `GITHUB_TOKEN` 是必要值
- `GITHUB_MODELS_ENDPOINT` 可選，預設是 `https://models.github.ai/inference`
- `GITHUB_MODEL` 可選，預設是 `openai/gpt-4.1-mini`

這樣做的好處是，後面其他函式都不需要直接碰環境變數，只要吃 `DemoOptions` 即可。

如果你以後要支援不同模型，通常第一個修改點就是這裡。

### CreateGitHubModelsChatClient()

這個函式負責把 GitHub Models 包裝成這個範例可用的 `OpenAIChatClient`。

因為 GitHub Models 提供 OpenAI 相容端點，所以這裡使用：

- `ApiKeyCredential(options.GitHubToken)`
- `OpenAIClientOptions { Endpoint = new Uri(options.Endpoint) }`

建立出來之後，再透過 `.AsIChatClient()` 轉成 `IChatClient`。

這一步很關鍵，因為後面的 Agent 與 Workflow 都共用同一個 `IChatClient` 抽象。

也就是說，這支程式的設計不是把模型邏輯散落到每個地方，而是先把模型接好，再交給不同流程使用。

## 單次執行與 REPL 是怎麼分流的

主程式透過 `args.Length` 判斷目前使用方式：

- 有命令列參數：把所有參數串成一段旅遊需求，直接呼叫 `RunScenarioAsync(..., WorkflowSelection.Both)`
- 沒有命令列參數：進入 `RunReplAsync()`

### 為什麼要有 REPL

REPL 的好處是，你不用每測一次需求就重開一次程式。

在這個範例裡，REPL 讓你可以：

- 反覆測不同旅遊需求
- 隨時切換執行模式
- 觀察 Agent 與 Workflow 在不同輸入下的差異

### RunReplAsync() 在做什麼

`RunReplAsync()` 是一個典型的互動式迴圈：

1. 先設定預設模式為 `WorkflowSelection.Both`
2. 呼叫 `WriteReplWelcome()` 顯示歡迎文字
3. 反覆讀取使用者輸入
4. 根據輸入判斷是一般需求、模式切換指令、說明指令，還是離開指令

裡面有幾個值得注意的點：

- 直接按 Enter 會使用 `GetDefaultTravelRequest()` 內建範例
- `/mode agent`、`/mode workflow`、`/mode both` 由 `TryParseModeCommand()` 處理
- `/help` 會顯示可用指令
- `/exit` 與 `/quit` 會結束 REPL

如果你想加新指令，例如 `/mode workflow-only-fast` 或 `/sample kaohsiung`，這裡就是主要修改點。

## RunScenarioAsync() 為什麼是整個範例的中樞

很多人第一次讀這支程式，會把注意力放在 Agent 或 Workflow API 本身。但更值得先看的是 `RunScenarioAsync()`。

原因是：它定義了這個範例的執行骨架。

它做的事情包括：

1. 建立整次請求的 `Stopwatch`
2. 呼叫 `WriteHeader()` 印出模型、端點、使用者需求
3. 根據 `WorkflowSelection` 決定要跑哪個流程
4. 在每個流程前後呼叫 `WriteWorkflowBoundary()`
5. 用 `WriteSection()` 印出 Agent 或 Workflow 的結果
6. 最後再印一次整體說明與總耗時

這個函式的重點在於：

- 它不負責生成行程內容
- 它負責安排「什麼時候呼叫誰」

這是一個很典型的協調層角色。

如果你未來想在 Agent 與 Workflow 中間再插入別的東西，例如人工確認、快取、審核節點，通常會先從這裡切入。

## Agent 路線是怎麼寫出來的

`RunAgentDemoAsync()` 展示的是「讓模型自己做比較多判斷」的寫法。

### Step 1: 建立工具清單

一開始先用 `AIFunctionFactory.Create(...)` 把三個普通 C# 函式包成 Agent 可呼叫的工具：

- `GetTransitAdvice`
- `GetPackingAdvice`
- `GetBudgetTip`

這三個函式原本只是一般方法，但透過 `AIFunctionFactory.Create(...)` 後，模型可以在需要時呼叫它們。

這是 Agent 設計裡很重要的一點：

工具不一定很複雜，它可以只是你原本就會寫的普通程式邏輯。

### Step 2: 建立可呼叫工具的 ChatClient

接著透過：

- `new ChatClientBuilder(baseChatClient)`
- `.UseFunctionInvocation()`
- `.Build()`

建立一個支援 function invocation 的 `IChatClient`。

意思是：後續 Agent 在和模型互動時，如果模型決定要用工具，這個 client 知道怎麼接住與執行。

### Step 3: 把 client、指示、工具組成 Agent

接著呼叫 `toolEnabledClient.AsAIAgent(...)`，把幾種東西包在一起：

- `instructions`
- `name`
- `description`
- `tools`

這段 `instructions` 才是 Agent 的人格與回覆策略來源。它告訴模型：

- 你是一個週末旅遊行程規劃助手
- 要先摘要，再安排 Day 1 / Day 2
- 條件不完整時要補合理假設
- 需要時才呼叫工具
- 用繁體中文回覆

這就是 Agent 版本的核心特徵：

你不是把每一步寫死，而是提供一組可工作的上下文，讓模型決定怎麼走。

### Step 4: 建立 Session

`agent.CreateSessionAsync()` 的作用是建立這次互動的 session。

你可以把它理解成「這次對話的上下文容器」。

即使這個範例每次只跑單次任務，依然保留 session 概念，因為這是 Agent 類型 API 常見的使用方式。

### Step 5: 執行 Agent

最後透過 `agent.RunAsync(...)` 真正開始執行。

傳入的東西有：

- 使用者需求 `request`
- 剛建立的 `session`
- `ChatClientAgentRunOptions`，裡面帶 `ModelId`

執行完後，程式會從 `response.Messages.LastOrDefault()?.Text` 取出最後回覆內容，並交給 `BuildExecutionResult()` 補上各階段耗時。

### 為什麼 Agent 路線看起來比較短

因為它把很多決策交給模型處理了，例如：

- 回覆應該長什麼樣子
- 什麼時候該用工具
- 要不要補假設

所以程式碼較少，但模型自由度較高。

## Workflow 路線是怎麼寫出來的

`RunWorkflowDemoAsync()` 展示的是另一種思路：

不要讓模型決定流程，而是先由程式把流程切好，再讓模型只在局部步驟工作。

### Step 1: 用 FunctionExecutor 定義每個步驟

這裡定義了五個步驟：

- `NormalizeRequest`
- `BudgetGuard`
- `PlanAttractions`
- `PlanMeals`
- `ComposeItinerary`

每個步驟都用 `FunctionExecutor<TInput, TOutput>` 表示。

這裡的重點不是語法，而是責任邊界：

- 哪一步要把自然語言拆成欄位
- 哪一步要做規則檢查
- 哪一步才交給模型產出內容
- 哪一步由程式負責最後整併

### Step 2: 什麼步驟交給普通程式，什麼步驟交給模型

這支範例很刻意地示範了一個實務觀念：

- `NormalizeRequest` 用 `ParseRequest()`，屬於一般字串解析
- `BudgetGuard` 用 `ApplyBudgetGuardrails()`，屬於規則判斷
- `PlanAttractions` 與 `PlanMeals` 才真的呼叫模型
- `ComposeItinerary` 用 `ComposeWorkflowResult()` 組裝最終輸出

這樣的拆法讓你清楚看到：

Workflow 並不是每一步都要靠 LLM。

真正穩定、可預測的地方，通常應該留給一般程式處理。

### Step 3: 用 WorkflowBuilder 串起步驟順序

接著用 `WorkflowBuilder(normalize)` 建立流程，並透過 `.AddEdge(...)` 明確指定順序：

1. `normalize -> budgetGuard`
2. `budgetGuard -> attractions`
3. `attractions -> meals`
4. `meals -> compose`

最後再用 `.WithOutputFrom(compose)` 指定最終輸出節點。

這裡就是 Workflow 與 Agent 最大的差別之一：

- Agent 的下一步偏向由模型決定
- Workflow 的下一步由你在程式裡明確定義

### Step 4: 實際執行 Workflow

流程建立好之後，透過 `InProcessExecution.RunStreamingAsync(workflow, request)` 執行。

回傳的是 `StreamingRun`，接著程式透過 `await foreach (WorkflowEvent evt in run.WatchStreamAsync())` 觀察事件流。

這裡主要處理三種事件：

- `WorkflowOutputEvent`：拿最終文字輸出
- `WorkflowErrorEvent`：流程層級錯誤
- `ExecutorFailedEvent`：某個 executor 執行失敗

這個設計有一個教學價值：

Workflow 不只是「跑一串函式」，它還有事件可觀察。這意味著日後要補監控、記錄、錯誤處理時，結構會比單一 Agent 更清楚。

## 進度列與耗時是怎麼接進去的

這個範例額外做了一層可觀測性，目的是讓讀者清楚看到每件事發生在哪個階段。

### WriteWorkflowBoundary()

這個函式負責印出：

- `[開始] 進入 Agent 流程`
- `[完成] 離開 Workflow 流程`

它不是框架功能，而是單純的主控台輸出封裝。

### ExecuteStageAsync() 與 ExecuteStageValueAsync()

這兩個函式是這支程式裡一個很實用的設計。

它們的用途是：

- 在階段開始前印出訊息
- 用 `Stopwatch` 計時
- 執行真正的工作
- 成功後記錄 `StageTiming`
- 失敗時印出失敗階段與已耗時間

差別只在於：

- `ExecuteStageAsync()` 包的是 `Task<T>`
- `ExecuteStageValueAsync()` 包的是 `ValueTask<T>`

這一層讓 Agent 與 Workflow 都能用同一套「開始 / 完成 / 失敗 / 耗時」輸出模式。

### BuildExecutionResult()

這個函式把真正產出的內容與階段耗時摘要組裝在一起。

因此最後看到的結果不是只有模型回答，還會包含：

- 每個階段花多久
- 總共花多久

如果你未來想把結果改成 JSON、表格，或輸出到檔案，這裡會是主要切入點之一。

## 這支程式裡，哪些是 Agent Framework，哪些不是

如果你是初學者，最容易混淆的是：哪些部分是框架的概念，哪些只是這個範例自己的 C# 寫法。

可以這樣分：

### 比較接近 Agent Framework 的部分

- `AsAIAgent(...)`
- `ChatClientAgent`
- `AgentSession`
- `agent.RunAsync(...)`
- `FunctionExecutor<TInput, TOutput>`
- `WorkflowBuilder`
- `InProcessExecution.RunStreamingAsync(...)`
- `WorkflowEvent`

### 比較接近一般應用程式設計的部分

- `DemoOptions`
- `ParseRequest()` 與 `ExtractXxx()`
- `ApplyBudgetGuardrails()`
- `ComposeWorkflowResult()`
- `WriteHeader()`、`WriteSection()`
- `FormatElapsed()`
- `RunReplAsync()` 的互動流程控制

這個區分很重要，因為你會發現：

真正讓範例可讀、可維護的，不只是框架 API，而是你有沒有把一般應用邏輯與 AI 部分分清楚。

## 未來最常改的地方

### 想換模型或端點

優先看：

- `DemoOptions.FromEnvironment()`
- `CreateGitHubModelsChatClient()`

這兩個地方決定模型 ID、token 與 endpoint 怎麼來。

### 想新增 Agent 工具

優先看：

- `RunAgentDemoAsync()` 裡的 `tools` 清單
- 對應的工具函式本體，例如 `GetTransitAdvice()`

做法通常是：

1. 先新增一個普通 C# 函式
2. 用 `AIFunctionFactory.Create(...)` 加到 `tools`
3. 視需要補上 `Description` 與參數描述

### 想新增 Workflow 步驟

優先看：

- `RunWorkflowDemoAsync()`

做法通常是：

1. 新增一個 `FunctionExecutor<TInput, TOutput>`
2. 決定它的輸入與輸出型別
3. 用 `WorkflowBuilder.AddEdge(...)` 把它接進流程
4. 如果它會改變最終輸出，確認 `TripPlanState` 是否要加欄位

### 想修改 REPL 指令

優先看：

- `RunReplAsync()`
- `TryParseModeCommand()`
- `SelectionLabel()`
- `WriteReplHelp()`

這幾個函式共同決定 REPL 的互動體驗。

### 想改輸出格式或耗時顯示

優先看：

- `WriteSection()`
- `WriteStageStatus()`
- `WriteStageFailure()`
- `BuildExecutionResult()`
- `FormatElapsed()`

如果你想把目前偏示範型的輸出，改成更正式的終端報表，這裡就是主要落點。

## 讀完之後，應該記住什麼

如果你只想帶走最重要的三件事，可以記住下面這些：

1. 這支程式先把模型連線建立好，再讓 Agent 與 Workflow 共用同一個 `IChatClient`。
2. Agent 路線強調讓模型自己決定比較多事情，Workflow 路線強調由程式先定義好步驟與責任。
3. 這個範例真正可維護的關鍵，不在於用了多少 AI API，而在於哪些地方交給模型、哪些地方保留給普通 C# 邏輯。

當你之後要修改這支程式時，也建議維持同一個原則：

- 該固定的流程，就寫成普通函式或 Workflow 步驟
- 該交給模型判斷的部分，再用 Agent 或模型呼叫處理

這樣程式才會持續可讀、可控，而且不會把所有事情都變成黑盒子。