# Microsoft Agent Framework + GitHub Models 多回合記憶範例

這個範例示範如何在一個極簡的 `.NET` 主控台應用程式中，使用 Microsoft Agent Framework 建立「多回合對話 + 僅限本次執行期間的記憶」。

這裡的記憶只存在於同一個 `AgentSession` 內：

- 同一次執行中，只要沿用同一個 session，Agent 就能記得前面聊過的內容。
- 執行 `/reset` 之後，會建立新的 session，先前對話記憶就會消失。
- 關閉程式再重新啟動，也不會保留任何記憶。

這種做法很適合拿來寫技術文章，因為結構簡單，重點明確，而且不需要引入資料庫或其他持久化儲存。

## 架構重點

- 使用 `ChatClient.AsAIAgent(...)` 建立單一 agent。
- 使用 `AgentSession` 保留多回合對話上下文。
- 使用 GitHub Models 的 OpenAI 相容 chat completions 端點：
  - `https://models.github.ai/inference`
- API key 從環境變數 `GITHUB_TOKEN` 讀取。
- 模型 ID 從 `GITHUB_MODEL` 讀取；若未設定，預設使用 `openai/gpt-4.1`。

## 需求

- .NET SDK 10
- 可呼叫 GitHub Models inference API 的 `GITHUB_TOKEN`

GitHub 官方文件：

- Microsoft Learn: [Agent Framework Overview](https://learn.microsoft.com/zh-tw/agent-framework/overview/?pivots=programming-language-csharp)
- Microsoft Learn: [Conversation Storage](https://learn.microsoft.com/en-us/agent-framework/agents/conversations/storage)
- Microsoft Learn: [OpenAI-Compatible Endpoints](https://learn.microsoft.com/en-us/agent-framework/integrations/openai-endpoints)
- GitHub Docs: [REST API endpoints for models inference](https://docs.github.com/en/rest/models/inference)

## 環境變數

PowerShell：

```powershell
$env:GITHUB_TOKEN = "your-token"
$env:GITHUB_MODEL = "openai/gpt-4.1"
```

`GITHUB_MODEL` 是可選的，不設定時會使用 `openai/gpt-4.1`。

## 執行方式

```powershell
dotnet restore
dotnet run
```

啟動後可直接進行多回合對話。

內建指令：

- `/summary`：要求 Agent 整理目前 session 中談過的內容
- `/reset`：建立新的 session，清空目前記憶
- `/exit`：結束程式

## 互動示例

```text
你> 我最近在準備一篇介紹 Agent 記憶的技術文章，想用 C# 當範例。

Agent> 好的，我記住了。你正在準備一篇介紹 Agent 記憶的技術文章，並且希望用 C# 作為示範語言。

你> 文章想強調不做持久化，只在同一次執行中記住對話。

Agent> 了解，這篇文章的重點是 session 內記憶，而不是跨程式重啟的持久化記憶。

你> /summary

Agent> 1. 主題摘要
你正在準備一篇介紹 Agent 記憶的技術文章，並希望使用 C# 示範。

2. 使用者提供過的重要資訊
- 文章要說明 Agent 記憶
- 想採用 C# 範例
- 重點是不做持久化，只保留同一次執行中的記憶

3. 尚未解決或可繼續追問的事項
- 文章是否還要補上 session reset 的示範
- 是否需要展示記憶限制與適用場景
```

## 為什麼這算是 Session Memory

這個範例沒有把任何對話寫入檔案、資料庫或快取系統。

Agent 能「記得」前文，是因為：

- 每次呼叫 `agent.RunAsync(...)` 時，都沿用同一個 `AgentSession`
- Agent Framework 會把這個 session 的對話歷史保留在本次執行流程中
- 因此模型在下一輪回應時，能看到前面回合的上下文

也就是說，這裡的記憶是：

- 有記憶性
- 可多回合延續
- 不持久化
- 只存在於目前 session

## 限制

- 如果對話很長，仍然會受到模型上下文長度限制。
- 這個範例沒有加上摘要壓縮、記憶裁切或外部儲存。
- 如果想做到跨程式啟動保留記憶，就需要另外加上持久化機制。

## 專案檔案

- `Program.cs`：主控台 REPL 與 Agent / Session 建立邏輯
- `docs/implementation-plan.md`：本範例的實作計畫

## 文章撰寫建議

如果你要把這個範例改寫成技術文章，可以沿著下面順序來寫：

1. 先說明什麼是多回合對話記憶
2. 再區分 session memory 與 persistent memory
3. 接著介紹 `AgentSession` 在這個範例中的角色
4. 最後用 `/summary` 與 `/reset` 展示「記得」與「忘記」兩種效果
