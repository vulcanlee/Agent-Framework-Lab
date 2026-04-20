# 快速開始

這個範例是一個 console app，用同一份旅遊需求，同時示範 Agent 與 Workflow 兩種設計方式。

## 1. 設定環境變數

必要項目：

- `GITHUB_TOKEN`

可選項目：

- `GITHUB_MODEL`
  - 預設 `openai/gpt-4.1-mini`
- `GITHUB_MODELS_ENDPOINT`
  - 預設 `https://models.github.ai/inference`

PowerShell：

```powershell
$env:GITHUB_TOKEN = "<your-token>"
$env:GITHUB_MODEL = "openai/gpt-4.1-mini"
```

## 2. 執行範例

```powershell
dotnet run --project .\TravelPlanner.Console\TravelPlanner.Console.csproj
```

不帶參數時會進入 REPL 模式。這代表你可以在同一個程式執行期間，連續測試多筆旅遊需求，而不需要每次重新啟動。

如果不輸入參數，程式會使用預設需求：

- 兩天一夜台中週末旅行
- 兩人同行
- 每人預算 3500 元
- 想安排咖啡店與在地美食

也可以自行帶入需求：

```powershell
dotnet run --project .\TravelPlanner.Console\TravelPlanner.Console.csproj -- "幫我規劃高雄一日散步行程，兩人同行，每人預算 1800 元，不開車"
```

## 3. REPL 模式操作

REPL 下可用指令：

- `/help`
- `/mode agent`
- `/mode workflow`
- `/mode both`
- `/exit`

直接輸入文字會把該文字視為新的旅遊需求。
直接按 Enter 則會使用內建預設範例。

## 4. 觀察重點

輸出會分成兩塊：

1. Agent 結果
2. Workflow 結果

除此之外，現在也會看到執行進度與耗時資訊：

- 進入某個流程之前的提示
- 離開某個流程之後的提示
- 每個階段開始與完成的提示
- 每個階段花費多久
- 本次整體花費多久

你可以用同一段需求比較：

- Agent 是否會主動補假設
- Agent 是否會決定呼叫工具
- Workflow 是否按照固定步驟產出內容
- Workflow 是否更容易看出每一步的責任邊界

## 5. 核心實作位置

主要程式在：

- `TravelPlanner.Console/Program.cs`

程式內含：

- GitHub Models 連線建立
- Agent 工具定義
- Agent 執行
- WorkflowBuilder 組裝
- FunctionExecutor 步驟定義
- REPL 互動迴圈
- 階段進度與耗時計時

## 6. 什麼時候算成功

成功執行時，你應該看到：

- 目前使用的模型與端點
- 使用者需求
- 進入與離開 Agent / Workflow 的提示訊息
- 各階段耗時與本次總耗時
- Agent 產生的旅遊建議
- Workflow 分步驟組裝出的旅遊建議

如果 `GITHUB_TOKEN` 沒有設定，程式會直接提示缺少該環境變數。