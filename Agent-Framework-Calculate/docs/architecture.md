# 架構說明

這個專案是一個小型但完整的 Agent CLI，重點是把「模型規劃 + 工具執行 + 可觀測性」清楚拆開。

## 核心元件
- `Program.cs`
  - 設定 console UTF-8。
  - 建立 logger、tools、agent factory、application。
- `ConsoleCalculatorApplication`
  - 負責 CLI 互動。
  - 顯示啟動訊息、讀取使用者輸入、呼叫 agent、輸出最終答案。
- `AppConfiguration`
  - 從環境變數讀取 `GITHUB_TOKEN`。
  - 固定使用 GitHub Models endpoint 與 `openai/gpt-4.1`。
- `GitHubModelsAgentFactory`
  - 建立 `OpenAIClient` 與 `ChatClientAgent`。
  - 註冊 `add`、`subtract`、`multiply`、`divide` 工具。
  - 透過 `UseFunctionInvocation()` 啟用自動工具呼叫迴圈。
  - 在 request/response 邊界輸出 `>>>`、`<<<` 與 token usage。
- `CalculatorTools`
  - 真正執行四則運算。
  - 在工具前後輸出 `<<< TOOL CALL REQUEST`、`<<< TOOL RESULT`。
  - 在工具完成後輸出 `???` 推進說明。
- `InteractionLogFormatter`
  - 統一格式化 log。
  - 處理多行 prefix、token usage 顯示與敏感資訊遮蔽。
- `agent-instructions.md`
  - 提供 runtime prompt。
  - 定義 Agent 只能處理四則運算、必須用工具、不直接心算。

## 資料流
1. 使用者在 console 輸入問題。
2. `ConsoleCalculatorApplication` 記錄 `>>> USER INPUT`。
3. `GitHubModelsAgentFactory` 送出 system prompt 與對話內容給 LLM。
4. 若 LLM 回傳 tool call，框架呼叫 `CalculatorTools`。
5. `CalculatorTools` 執行本地計算並記錄工具 log。
6. 工具結果被帶回對話上下文，Agent 再次呼叫 LLM。
7. 當最新一輪回應不再包含 tool call，而是一般文字回答時，流程停止。
8. `ConsoleCalculatorApplication` 輸出 `<<< FINAL RESPONSE` 與最終自然語言答案。

## 停止條件
- 停止的真正條件不是看到某段特定字串。
- 判斷依據是最新一輪 LLM 回傳中是否還有 `FunctionCallContent`。
- 有新的 tool call 就繼續。
- 沒有新的 tool call，只有一般文字回答就停止。

## 設計取向
- 工具負責計算，模型負責規劃。
- CLI 與 logger 負責把過程透明化。
- prompt、文件、測試需保持一致，避免「實作是一套、文件是另一套」。
