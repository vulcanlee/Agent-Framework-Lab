# Agent Framework Workflow Lab

這是一個用繁體中文撰寫的 Microsoft Agent Framework 入門範例，主題是「週末旅遊行程規劃助手」。

這個專案刻意把同一個生活化情境拆成兩種做法：

1. Agent 做法：讓模型在一個較完整的提示詞下，自行決定是否呼叫工具、如何整理回覆。
2. Workflow 做法：把流程拆成明確步驟，由開發者決定執行順序與責任分工。

這樣的安排很適合第一次接觸 Microsoft Agent Framework 的讀者，因為可以直接比較：

- 什麼時候用 Agent 比較省事
- 什麼時候應該改成 Workflow 來提高可預測性
- 工具呼叫與固定流程在設計上的差異

## 專案內容

- `TravelPlanner.Console/Program.cs`
  - 一個可直接執行的 console sample
  - 內含 Agent 示範與 Workflow 示範
- `docs/getting-started.md`
  - 快速啟動方式
- `docs/agent-vs-workflow.md`
  - Agent 與 Workflow 的設計差異
- `docs/scenario-walkthrough.md`
  - 旅遊規劃情境與執行流程說明

## 技術選擇

- Framework: Microsoft Agent Framework for .NET
- Workflow API: `WorkflowBuilder` + `FunctionExecutor`
- Chat abstraction: `IChatClient`
- Model provider: GitHub Models
- Endpoint: `https://models.github.ai/inference`
- Authentication: 使用環境變數 `GITHUB_TOKEN`

本範例也使用 `Microsoft.Extensions.AI.OpenAI`，透過 OpenAI 相容介面把 GitHub Models 接到 Agent Framework。

## 先決條件

需要先準備：

1. .NET SDK
2. 可用的 GitHub Models 權限
3. 環境變數 `GITHUB_TOKEN`

可選環境變數：

- `GITHUB_MODEL`
  - 預設值是 `openai/gpt-4.1-mini`
- `GITHUB_MODELS_ENDPOINT`
  - 預設值是 `https://models.github.ai/inference`

PowerShell 範例：

```powershell
$env:GITHUB_TOKEN = "<your-token>"
$env:GITHUB_MODEL = "openai/gpt-4.1-mini"
```

## 執行方式

```powershell
dotnet run --project .\TravelPlanner.Console\TravelPlanner.Console.csproj
```

不帶參數時，程式會進入 REPL 模式。
你可以重複輸入不同旅遊需求，並在每次執行前後看到：

- 目前準備進入哪個流程
- 每個階段的開始與完成訊息
- 每個階段耗時
- 本次總耗時

也可以直接帶入一段需求：

```powershell
dotnet run --project .\TravelPlanner.Console\TravelPlanner.Console.csproj -- "幫我規劃兩天一夜台中旅行，兩人同行，每人預算 3500 元，想吃在地美食與咖啡店"
```

## REPL 指令

進入 REPL 後，可使用：

- `/help`
- `/mode agent`
- `/mode workflow`
- `/mode both`
- `/exit`

其中：

- `agent` 只跑 Agent 流程
- `workflow` 只跑 Workflow 流程
- `both` 依序跑 Agent 與 Workflow

## 你會看到什麼

程式會輸出三個部分：

1. Agent 結果
2. Workflow 結果
3. 如何閱讀這個範例

在 REPL 模式下，還會額外看到：

- `[開始] 進入 Agent 流程`
- `[Agent] 開始階段: 建立 Agent Session`
- `[Workflow] 完成階段: 景點規劃，耗時 1.23 秒`
- `本次總耗時: 4.85 秒`

同一個需求下：

- Agent 版本偏向「把問題交給一個會思考與呼叫工具的助理」
- Workflow 版本偏向「把任務切成需求正規化、預算檢查、景點規劃、餐食規劃、最後組裝」

## 這個範例想傳達的重點

如果需求本質上只是固定步驟串接，Workflow 通常比 Agent 更容易控管。

如果需求有大量模糊判斷、需要工具選擇或希望互動更自然，Agent 會更省力。

實務上常見的做法不是二選一，而是：

- 在入口用 Agent 理解需求
- 在核心執行區用 Workflow 保持流程穩定

## 文件導覽

- [docs/getting-started.md](docs/getting-started.md)
- [docs/agent-vs-workflow.md](docs/agent-vs-workflow.md)
- [docs/scenario-walkthrough.md](docs/scenario-walkthrough.md)