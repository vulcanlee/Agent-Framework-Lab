# AI Topic Pulse CLI

這是一個以 C# 撰寫的教學型範例，示範如何依照 Microsoft Agent Framework 的 OpenAI provider 文件思路，結合 OpenAI Responses API 的 `Code Interpreter`，分析最近 24 小時內與 AI 主題相關的熱門討論。

整體設計刻意保持簡單：

- `C# CLI`
- `Hacker News + GitHub + Reddit` 三個資料來源
- `Code Interpreter` 負責分析與排名
- 螢幕即時顯示每個階段的狀態訊息

## 情境

程式會抓取最近 24 小時內與 AI 有關的熱門內容，來源包含：

- Hacker News
- GitHub 搜尋結果
- Reddit 搜尋結果

取得資料後，C# 程式先把不同來源整理成同一個 JSON dataset，再交給 `Code Interpreter` 進行：

- 去重
- 排名
- 產出前 10 名
- 生成繁體中文摘要

## Where Code Interpreter Is Used

這個範例刻意把責任切成兩段，方便技術文章清楚展示 `Code Interpreter` 的價值。

### 由 C# 負責

- 讀取環境變數 `OpenAI_Key`
- 呼叫 Hacker News、GitHub、Reddit API
- 把不同來源資料統一成 `TopicMention`
- 建立單一 JSON dataset
- 顯示每個來源 agent 的狀態訊息

### 由 Code Interpreter 負責

- 讀取 JSON dataset
- 用 Python 進行資料分析
- 去重與排序
- 產出前 10 名熱門 AI 話題
- 生成繁體中文摘要
- 回傳最終 markdown 報告
- 在執行時回報分析階段的狀態

## 螢幕上的狀態訊息

執行 CLI 時，螢幕會即時顯示類似下列訊息：

```text
[status] Loading configuration.
[status] [Hacker News Agent] Fetching recent AI discussions.
[status] [Hacker News Agent] Collected 10 items.
[status] [GitHub Agent] Fetching recent AI discussions.
[status] [Reddit Agent] Fetching recent AI discussions.
[status] [Analysis Agent] Building dataset.
[status] [Analysis Agent] Starting analysis.
[status] [Analysis Agent] Analysis request created.
[status] [Analysis Agent] Code Interpreter started.
[status] [Analysis Agent] Code Interpreter is analyzing the dataset.
[status] [Analysis Agent] Code Interpreter finished running.
[status] [Analysis Agent] Analysis complete.
```

這些訊息會幫你看見：

- 每個來源 agent 正在做什麼
- 哪個來源成功或失敗
- Analysis Agent 是否已開始工作
- Code Interpreter 目前是否正在分析資料

## 為什麼同時提到 Agent Framework 與 Responses API

這個專案參考 Microsoft Agent Framework 的 OpenAI provider 與 Code Interpreter 文件來設計結構，並使用 `Microsoft.Agents.AI.OpenAI` 套件。

不過目前 C# 版 `AsAIAgent(...)` 與 OpenAI Responses hosted `Code Interpreter` tool 型別在工具接合上無法直接串起來。因此本專案採用：

- 專案架構與設計思路依照 Agent Framework 文件
- 真正啟用 `Code Interpreter` 的分析步驟，直接使用 OpenAI .NET Responses SDK

這樣可以兼顧：

- 範例真的可執行
- 程式結構仍然簡潔
- 技術文章可以誠實說明現況

## 專案結構

```text
src/AiTopicPulse.Cli
tests/AiTopicPulse.Cli.Tests
docs/implementation-plan.md
README.md
```

## 需求

- .NET 10 SDK
- OpenAI API Key

## 設定方式

PowerShell:

```powershell
$env:OpenAI_Key="your-openai-api-key"
```

## 執行方式

```powershell
dotnet run --project .\src\AiTopicPulse.Cli
```

## 測試

```powershell
dotnet test .\AgentFrameworkSkillCodeInterpreter.slnx
```

## 輸出內容

程式會在終端機輸出：

- 即時狀態訊息
- 來源成功 / 失敗狀態
- Top 10 AI topics
- 整體觀察摘要
- Code Interpreter trace（若模型回傳相關內容）

## 重要限制

- 第一版主題固定為 `AI`
- 第一版只做 CLI，不做 Web API
- 第一版只輸出到終端機
- 若某個來源失敗，只要仍有來源成功就會繼續分析

## 參考文件

- [Microsoft Agent Framework Overview](https://learn.microsoft.com/zh-tw/agent-framework/overview/?pivots=programming-language-csharp)
- [OpenAI Agents provider](https://learn.microsoft.com/zh-tw/agent-framework/agents/providers/openai)
- [Code Interpreter](https://learn.microsoft.com/zh-tw/agent-framework/agents/tools/code-interpreter?pivots=programming-language-csharp)
