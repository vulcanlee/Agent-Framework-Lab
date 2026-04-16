# Microsoft Agent Framework 結構化輸出 CLI 專案計畫

## 專案摘要

本專案建立一個以 C# Console / CLI 為主的教學型範例，主題是「會議行動清單產生器」。使用者可以在命令列中輸入會議內容、指定文字檔、透過管線傳入文字，或在沒有參數時進入互動式 REPL，然後由 Microsoft Agent Framework 搭配 GitHub Models 產生結構化輸出。

這個範例刻意維持簡單，目標不是展示大型架構，而是提供一個能夠直接拿來寫技術文章的最小可用案例。文章焦點會放在：

1. 如何建立最小可用的 Agent。
2. 如何透過 structured output 回傳強型別資料。
3. 如何把 CLI 與 REPL 輸入模式整合到同一套 Agent 邏輯。
4. 如何透過 `GITHUB_TOKEN` 串接 GitHub Models inference endpoint。

## 應用情境

應用情境固定為「會議行動清單產生器」。

輸入是一段非結構化的會議逐字稿或會議摘要，輸出則是方便後續追蹤的結構化資料，包含：

- 會議標題
- 會議摘要
- 關鍵決策
- 行動項目
- 風險
- 待確認問題

這個情境有幾個優點：

- 真實且常見，讀者容易理解。
- 欄位設計直觀，適合講解 structured output。
- 很容易從人類可讀文本映射到 JSON 結構。

## 技術設計

### 1. Agent 與模型存取

- 使用 Microsoft Agent Framework 的 `ChatClientAgent`。
- 使用 `RunAsync<T>` 直接回傳強型別結構化結果。
- 下層模型 client 採用 `OpenAI.Chat.ChatClient`。
- 透過 `Microsoft.Extensions.AI.OpenAI` 轉成 `IChatClient`，再建立 Agent。
- GitHub Models inference endpoint 使用 `https://models.github.ai/inference/` 作為 base URL。
- API Token 固定從環境變數 `GITHUB_TOKEN` 讀取。

### 2. 輸入模式

支援四種入口：

- `sample`
- `file <path>`
- `stdin`
- 無參數啟動時進入 REPL

其中 REPL 支援：

- 載入內建範例
- 載入檔案
- 貼上多行會議內容
- 檢視與清除當前輸入
- 切換模型
- 切換 JSON 輸出
- 執行目前內容

### 3. 輸出模式

支援兩種輸出模式：

- 預設人類可讀摘要
- JSON 輸出

CLI 透過 `--json` 控制，REPL 則透過 `/json`、`/json on`、`/json off` 控制。

### 4. 預設模型

預設模型設為 `openai/gpt-4.1-mini`，因為它適合教學範例，也與 GitHub Models 的 OpenAI 相容端點格式一致。

CLI 可用 `--model <model-id>` 覆寫，REPL 可用 `/model <model-id>` 切換。

### 5. 資料模型

最上層輸出型別為 `MeetingActionPlan`，底下包含：

- `ActionItem`
- `RiskItem`
- `FollowUpQuestion`

欄位設計原則：

- 欄位名稱清楚、直觀。
- 可對應實際會議紀錄情境。
- 若資料未明確提供，允許 `null` 或保守值，不要求模型猜測。

## 實作流程

### CLI 模式

1. 解析命令列參數。
2. 從內建範例、檔案或 `stdin` 取得會議內容。
3. 建立 GitHub Models chat client。
4. 建立 Agent 並提供固定 instructions。
5. 呼叫 `RunAsync<MeetingActionPlan>` 要求 structured output。
6. 依照是否指定 `--json` 決定輸出摘要或 JSON。

### REPL 模式

1. 啟動後顯示簡短說明。
2. 使用者透過 `/sample`、`/file` 或 `/paste` 建立當前輸入。
3. 可先用 `/model`、`/json` 調整執行設定。
4. 用 `/run` 執行當前會議內容。
5. 可重複修改輸入並再次執行，不需要重新啟動程式。

## 錯誤處理

需要清楚處理以下情境：

- 未設定 `GITHUB_TOKEN`
- `file` 模式未提供檔案路徑
- 指定檔案不存在
- 輸入內容為空
- 無法辨識的 CLI 參數
- REPL 中尚未載入內容就執行 `/run`
- REPL 多行貼上後沒有實際內容

這些錯誤都要回傳容易理解的中文訊息，方便讀者在跟著文章操作時快速排查。

## 驗收與測試情境

### 功能驗收

- `sample` 模式可直接執行。
- `file` 模式可讀取 UTF-8 文字檔。
- `stdin` 模式可從標準輸入接收內容。
- 無參數啟動可進入 REPL。
- REPL 的 `/sample`、`/paste`、`/json`、`/model`、`/run` 可正常運作。
- `--json` 模式輸出可被 JSON 工具解析。
- `--model` 可覆寫預設模型名稱。

### 內容品質驗收

- 對正常逐字稿可整理出摘要、決策與待辦。
- 對沒有負責人或截止日的內容，不應捏造資訊。
- 對含有寒暄或噪音的內容，仍可萃取出核心行動項目與風險。

### 文件驗收

- `README.md` 提供快速上手與執行方式。
- `docs/plan.md` 提供完整設計背景與實作說明。
- 兩份文件中的 CLI 介面、REPL 指令、資料模型、輸出情境與注意事項保持一致。

## 文件同步原則

每次異動專案時，`README.md` 與 `docs/plan.md` 都必須同步更新。

- `README.md` 偏向快速導覽、安裝、執行與範例輸出。
- `docs/plan.md` 偏向完整設計、流程、資料模型與驗收條件。
- 若 CLI 參數、REPL 指令、輸出欄位、預設模型或情境改變，兩份文件都必須一起更新。

## UTF-8 要求

本專案新增的文字檔、程式碼檔與文件都採用 UTF-8 編碼，確保繁體中文內容在 Windows、GitHub 與編輯器中都能穩定顯示。

另外透過 `.editorconfig` 明確約束 `.cs`、`.md`、`.txt`、`.json`、`.csproj` 的編碼設定，讓後續維護時較不容易發生編碼漂移。
