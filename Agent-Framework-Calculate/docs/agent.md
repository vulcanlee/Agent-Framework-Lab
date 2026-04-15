# Agent.md

## Purpose
這是一個使用 Microsoft Agent Framework 與 GitHub Models 建立的四則運算 Agent。
它的主要用途是接收自然語言或算式輸入，透過工具呼叫完成加、減、乘、除運算，並把整個與 LLM 的互動流程清楚顯示在 console。

## Responsibilities
Agent 必須負責：

- 理解使用者輸入中的運算意圖
- 判斷是否需要呼叫工具
- 在需要時選擇正確的工具與參數
- 在工具結果返回後，決定是否需要再次與 LLM 互動
- 在可完成回答時輸出最終答案
- 將整個互動過程用可讀格式輸出到 console

## Supported Operations
目前只支援四則運算：

- `add(a, b)`
- `subtract(a, b)`
- `multiply(a, b)`
- `divide(a, b)`

## Tool Rules
- Agent 不可以直接心算或猜答案。
- 所有需要計算的地方，都必須透過工具完成。
- 若輸入可拆成多步，Agent 可分多輪工具呼叫完成。
- 工具結果必須被帶回上下文，再由 LLM 判斷下一步。
- 除以 0 必須視為錯誤。

## Iteration Rules
Agent 的多輪推理流程如下：

1. 將 system prompt 與使用者輸入送給 LLM
2. 若 LLM 回傳工具呼叫要求，執行對應工具
3. 將工具結果加入對話上下文
4. 再次把更新後的內容送給 LLM
5. 重複以上步驟，直到 LLM 不再要求工具呼叫，而是直接回覆答案

## Stop Condition
Agent 停止的真正依據不是看到某段文字，而是：

- 最新一輪 LLM 回傳中，不再包含新的工具呼叫要求
- 而是直接產生一般文字回答

換句話說：

- 有 tool call -> 繼續
- 沒有 tool call，只有最終回答 -> 停止

## Logging Format
所有 LLM 與工具互動都必須輸出到 console，格式如下：

- `>>>`：送往 LLM 的內容
- `<<<`：從 LLM 收到的內容
- `???`：再次推論前的詳細說明

### Required Output Sections
至少包含：

- `>>> SYSTEM PROMPT`
- `>>> USER MESSAGE`
- `>>> FOLLOW-UP PAYLOAD`
- `<<< TOOL CALL REQUEST`
- `<<< TOOL RESULT`
- `<<< LLM RESPONSE`
- `<<< FINAL RESPONSE`
- `<<< TOKEN USAGE`
- `??? REASON FOR NEXT INFERENCE`

## Detailed ??? Rule
每次工具執行完成，且系統準備再次送往 LLM 前，必須輸出 `???` 說明，內容至少包含：

- 剛剛完成的工具呼叫
- 目前得到的結果
- 為什麼現在不能直接結束
- 下一輪希望 LLM 決定什麼

### Example
```text
??? 已完成工具呼叫：multiply(a=2, b=3)
??? 目前已知結果：6
??? 為何需要再次推論：原始算式仍包含後續加法與除法
??? 下一輪目標：請 LLM 規劃下一個工具呼叫
```

## Example Flow
輸入：

```text
(2*3 加 10)/8
```

可能流程：

```text
>>> SYSTEM PROMPT: ...
>>> USER MESSAGE: (2*3 加 10)/8
<<< TOOL CALL REQUEST: multiply(a=2, b=3)
<<< TOOL RESULT: 6
??? 已完成工具呼叫：multiply(a=2, b=3)
??? 目前已知結果：6
??? 為何需要再次推論：原始算式尚未完成
??? 下一輪目標：請 LLM 決定下一個工具呼叫
>>> FOLLOW-UP PAYLOAD: ...
<<< TOOL CALL REQUEST: add(a=6, b=10)
<<< TOOL RESULT: 16
??? 已完成工具呼叫：add(a=6, b=10)
??? 目前已知結果：16
??? 為何需要再次推論：仍需完成最後一步除法
??? 下一輪目標：請 LLM 規劃最後一步
>>> FOLLOW-UP PAYLOAD: ...
<<< TOOL CALL REQUEST: divide(a=16, b=8)
<<< TOOL RESULT: 2
<<< FINAL RESPONSE: 答案是 2。
<<< TOKEN USAGE:
<<< Input tokens: 123
<<< Output tokens: 45
<<< Total tokens: 168
```

## Error Handling
- 若輸入不是四則運算，應明確拒答
- 若工具出錯，應將錯誤原因回傳給使用者
- 若無法取得 token usage，應明確顯示：
  - `無法取得 token usage。`

## Security
- 不可在任何 log 中輸出 `GITHUB_TOKEN`
- Bearer token 必須遮蔽
- 所有檔案與輸出皆應以 UTF-8 處理

## PowerShell UTF-8
若在一般 PowerShell 視窗直接讀取中文檔案時看到亂碼，通常是終端輸出編碼造成，而不是檔案本身損壞。專案提供以下腳本可快速切換到 UTF-8：

```powershell
. .\scripts\Enable-Utf8Console.ps1
```

切換後可用下面方式驗證：

```powershell
Get-Content .\docs\agent.md -Encoding utf8 | Select-Object -First 5
Get-Content .\docs\agent-calculator-plan.md -Encoding utf8 | Select-Object -First 5
Get-Content .\src\AgentFunctionCall\Prompts\agent-instructions.md -Encoding utf8 | Select-Object -First 5
```

## Runtime Source of Truth
目前真正影響執行行為的 runtime 規則仍來自程式中的 agent instructions 與 tool pipeline。
如果未來要讓這份文件成為 runtime prompt，需由程式在啟動時讀取本檔內容並注入 Agent 建立流程。
