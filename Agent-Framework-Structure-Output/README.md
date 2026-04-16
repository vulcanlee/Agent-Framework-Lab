# Microsoft Agent Framework 結構化輸出 CLI 範例

這個範例示範如何使用 Microsoft Agent Framework 建立一個簡單、真實又適合教學文章的 CLI 應用程式。情境是「會議行動清單產生器」：把會議逐字稿或會議摘要交給代理程式，讓它回傳強型別的結構化輸出，整理出摘要、決策、待辦、風險與待確認事項。

模型提供者使用 GitHub Models，推論端點是 `https://models.github.ai/inference`，API Token 從環境變數 `GITHUB_TOKEN` 讀取。

## 為什麼這個範例適合寫文章

- 情境真實，容易讓讀者理解 structured output 的價值。
- 程式骨架精簡，重點集中在 Agent、CLI 與輸出模型。
- 同時支援 CLI 參數模式與互動式 REPL，方便示範不同操作方式。
- 可切換人類可讀摘要與 JSON，方便文章同時展示結果與資料模型。

## 專案結構

```text
.
|-- Cli/
|-- Models/
|-- Services/
|-- docs/
|   `-- plan.md
|-- samples/
|   `-- meeting-transcript.txt
|-- Agent-Framework-Structure-Output.csproj
`-- README.md
```

## 先決條件

- .NET 9 SDK
- 一個可用的 GitHub Models Token
- 將 Token 放入環境變數 `GITHUB_TOKEN`

PowerShell 設定範例：

```powershell
$env:GITHUB_TOKEN = "ghp_your_token"
```

## 安裝與執行

還原套件：

```powershell
dotnet restore
```

### 1. 互動式 REPL

不帶參數啟動：

```powershell
dotnet run
```

進入 REPL 後可使用以下指令：

- `/help`：顯示指令說明。
- `/sample`：載入內建會議範例。
- `/file <path>`：載入 UTF-8 文字檔。
- `/paste`：進入多行貼上模式，輸入 `.end` 結束。
- `/show`：顯示目前會議內容。
- `/clear`：清除目前會議內容。
- `/model`：顯示目前模型。
- `/model <id>`：切換模型。
- `/json`、`/json on`、`/json off`：切換 JSON 輸出。
- `/run`：執行目前會議內容。
- `/exit`：離開 REPL。

建議示範流程：

```text
/sample
/json on
/run
```

### 2. CLI 參數模式

使用內建範例執行：

```powershell
dotnet run -- sample
```

輸出 JSON：

```powershell
dotnet run -- sample --json
```

讀取 UTF-8 檔案：

```powershell
dotnet run -- file samples/meeting-transcript.txt
```

從標準輸入讀取：

```powershell
Get-Content samples/meeting-transcript.txt | dotnet run -- stdin --json
```

指定模型：

```powershell
dotnet run -- sample --model openai/gpt-4.1-mini
```

查看說明：

```powershell
dotnet run -- --help
```

## 操作模式

### REPL 模式

當 `dotnet run` 不帶任何參數時，程式會直接進入互動式 REPL。這很適合教學情境，因為讀者可以先載入範例、貼上會議內容、切換 JSON 輸出，再執行代理程式，不需要一次記住所有命令列參數。

### CLI 模式

- `sample`：使用內建範例會議逐字稿。
- `file <path>`：讀取指定 UTF-8 文字檔。
- `stdin`：從標準輸入讀取內容。
- `--json`：輸出結構化 JSON。
- `--model <model-id>`：覆寫預設模型，預設為 `openai/gpt-4.1-mini`。

## 結構化輸出模型

最上層型別為 `MeetingActionPlan`，包含以下欄位：

- `meeting_title`
- `summary`
- `key_decisions`
- `action_items`
- `risks`
- `follow_up_questions`

其中 `action_items`、`risks`、`follow_up_questions` 分別對應：

- `ActionItem`
- `RiskItem`
- `FollowUpQuestion`

這樣的資料模型很適合文章示範，因為欄位直觀、真實且容易對照會議內容。

## 核心實作概念

1. 使用 `OpenAI.Chat.ChatClient` 指向 GitHub Models inference endpoint。
2. 透過 `Microsoft.Extensions.AI.OpenAI` 將 client 轉成 `IChatClient`。
3. 使用 `ChatClientAgent` 建立 Microsoft Agent Framework Agent。
4. 呼叫 `RunAsync<MeetingActionPlan>`，直接要求代理程式回傳強型別 structured output。
5. 在沒有 CLI 參數時，以 REPL 作為另一個輸入入口，重用同一套 Agent 與輸出邏輯。

## 範例輸出

摘要模式範例：

```text
會議主題：合作夥伴後台首頁上線準備

摘要
團隊聚焦於五月底前上線合作夥伴後台首頁，先處理儀表板與通知中心。儀表板 API、設計稿確認與 KPI 指標整理已分配負責人，但 CRM API 欄位凍結時間仍是主要風險。
```

JSON 模式範例：

```json
{
  "meeting_title": "合作夥伴後台首頁上線準備",
  "summary": "團隊聚焦於五月底前上線合作夥伴後台首頁...",
  "key_decisions": [
    "第一階段先做儀表板與通知中心"
  ],
  "action_items": [
    {
      "task": "完成儀表板 API 第一版並提供測試環境",
      "owner": "Nina",
      "due_date": "下週三",
      "priority": "高"
    }
  ],
  "risks": [
    {
      "risk": "CRM API 欄位尚未完全對齊且缺少凍結日期",
      "impact": "可能延誤資料整合與上線時程",
      "mitigation": "由 Amy 跟 CRM 團隊追蹤欄位凍結時間"
    }
  ],
  "follow_up_questions": [
    {
      "question": "通知中心需求細節是否已定稿？",
      "why_it_matters": "會影響後續開發排程"
    }
  ]
}
```

## 注意事項

- 如果 `GITHUB_TOKEN` 未設定，程式會直接失敗並提示設定方式。
- 如果輸入檔案不存在、內容為空，或 `stdin` 沒有資料，CLI 會回傳清楚錯誤訊息。
- REPL 模式下若尚未載入會議內容就執行 `/run`，會提示先使用 `/sample`、`/file` 或 `/paste`。
- 代理程式會盡量保守整理資訊，不會在未明確提及時捏造負責人與日期。
- 專案提供 `.editorconfig`，將 `.cs`、`.md`、`.txt`、`.json`、`.csproj` 統一為 UTF-8。

## 延伸方向

- 增加 `--output <path>`，將 JSON 寫入檔案。
- 讓 REPL 支援儲存最近一次輸出結果。
- 增加更多輸入情境，例如客服對話或需求訪談。
- 改造成 Minimal API，將 CLI 核心邏輯重用到 HTTP 介面。

完整設計與驗收規劃請參考 [docs/plan.md](docs/plan.md)。
