# Logging 與 Tracing

這個專案的可觀測性重點是：讓使用者能直接從 console 看出 Agent 與 LLM 如何互動、何時做工具呼叫、何時再次推論、何時停止。

## Prefix 規則
- `>>>`：送往 LLM 的內容
- `<<<`：從 LLM 或工具收到的內容
- `???`：系統準備再次推論前的外部說明

## 主要區塊
- `>>> USER INPUT`
- `>>> SYSTEM PROMPT`
- `>>> USER MESSAGE`
- `>>> FOLLOW-UP PAYLOAD`
- `<<< TOOL CALL REQUEST`
- `<<< TOOL RESULT`
- `<<< TOOL ERROR`
- `<<< LLM RESPONSE`
- `<<< FINAL RESPONSE`
- `<<< TOKEN USAGE`
- `??? 已完成工具呼叫`
- `??? 目前已知結果`
- `??? 為何需要再次推論`
- `??? 下一輪目標`

## 實際責任分工
- `ConsoleCalculatorApplication`
  - 輸出使用者輸入與最終回答
- `GitHubModelsAgentFactory`
  - 輸出送往 LLM 的 payload、收到的 response 與 token usage
- `CalculatorTools`
  - 輸出工具呼叫、工具結果、工具錯誤與再次推論說明
- `InteractionLogFormatter`
  - 統一多行前綴與內容遮蔽

## 停止條件與 log 的關係
- `<<< FINAL RESPONSE` 不是停止條件本身，而是停止條件成立後的展示。
- 真正的停止條件是 LLM 最新回應不再包含新的 tool call。
- 當 `FunctionCallContent` 為 0，且有一般文字回應時，系統將該回覆視為最終回答。

## Token Usage
- 至少顯示：
  - `Input tokens`
  - `Output tokens`
  - `Total tokens`
- 若 SDK 有提供，額外顯示：
  - `Cached input tokens`
  - `Reasoning tokens`
- 若無法取得 usage，顯示 `無法取得 token usage。`

## 遮蔽規則
- `Bearer <token>` 一律顯示為 `Bearer [REDACTED]`
- 不可在任何 log 中印出 `GITHUB_TOKEN`

## 維護注意事項
- 變更 prefix、區塊名稱或 usage 輸出時，要同步更新：
  - `docs/examples.md`
  - `docs/agent.md`
  - formatter 與工具相關測試
