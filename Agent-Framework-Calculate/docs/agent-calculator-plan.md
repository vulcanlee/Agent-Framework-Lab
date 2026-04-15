# 更新版 LLM 對話可視化計畫

## Summary
- 更新 `docs/agent-calculator-plan.md`，把 LLM 對話與多輪迭代過程的顯示規格改成「逐向度可讀」模式，而不只是一般區塊式 logging。
- 每次送往 LLM 的 payload 或文字內容，前面必須加上 `>>>`；每次從 LLM 收到的內容，前面必須加上 `<<<`。
- 每次在再次推論前，必須先輸出一段以 `???` 開頭的說明文字，且採詳細推進說明風格，明確交代為什麼要再送一次 LLM、目前已知結果是什麼、下一輪要解決什麼。
- 計畫文件本身與相關程式檔需明確要求使用 UTF-8，並把目前亂碼文件列入修正工作。

## Key Changes
- 更新既有計畫文件，將 LLM 可觀測性需求從一般 section logging 改為三種方向性標記：
  - `>>>`：所有送往 LLM 的內容
  - `<<<`：所有從 LLM 收到的內容
  - `???`：再次推論前的說明與推進理由
- 在計畫中明確要求顯示的內容至少包含：
  - `>>> SYSTEM PROMPT`
  - `>>> USER MESSAGE`
  - `>>> FOLLOW-UP PAYLOAD`
  - `<<< LLM RESPONSE`
  - `<<< TOOL CALL REQUEST`
  - `<<< TOOL RESULT`
  - `??? REASON FOR NEXT INFERENCE`
- `???` 說明需採詳細風格，至少包含：
  - 剛完成的工具呼叫與結果
  - 為何目前還不能直接結束
  - 下一輪要請 LLM 判斷或規劃的重點
  - 若是最後一輪，不輸出 `???`，直接輸出最終 `<<<` 內容
- 在計畫中補強 logging abstraction 規格：
  - 不能只接受 `title + content`
  - 需支援方向性輸出與多行 prefix 格式
  - 多行內容需逐行保留前綴，避免只有第一行帶 `>>>`、`<<<`、`???`
- 更新文件中的 UTF-8 規則：
  - `docs/agent-calculator-plan.md` 必須先修正亂碼，再追加新規格
  - `src/` 與 `docs/` 所有涉及中文與 console 顯示的檔案都必須以 UTF-8 儲存
  - console 輸出要驗證 `>>>`、`<<<`、`???` 前綴搭配中文內容不亂碼

## Public Interfaces / Behavior
- CLI 的可視化輸出格式改為明確區分方向：
  - `>>>` 表示送給 LLM
  - `<<<` 表示 LLM 回來的內容
  - `???` 表示系統準備再次推論的說明
- 範例流程應長成：
  - `>>> SYSTEM PROMPT: ...`
  - `>>> USER MESSAGE: (2*3 加 10)/8`
  - `<<< TOOL CALL REQUEST: multiply(a=2, b=3)`
  - `<<< TOOL RESULT: 6`
  - `??? 已取得 multiply 結果 6，但原始算式仍包含後續加法與除法，系統將帶著目前上下文再次送往 LLM，請其決定下一個工具呼叫。`
  - `>>> FOLLOW-UP PAYLOAD: ...`
  - `<<< TOOL CALL REQUEST: add(a=6, b=10)`
  - `<<< TOOL RESULT: 16`
  - `??? 已取得 add 結果 16，但整體算式尚未完成，系統將再次把結果帶回 LLM，請其規劃最後一步除法。`
  - `>>> FOLLOW-UP PAYLOAD: ...`
  - `<<< TOOL CALL REQUEST: divide(a=16, b=8)`
  - `<<< TOOL RESULT: 2`
  - `<<< FINAL RESPONSE: 結果是 2`
- 若 LLM 沒有要求工具而直接回答，也要保留 `>>>` 與 `<<<`，但不需要 `???`
- 若出現錯誤或中止，也要能看出最後一個 `>>>` payload、收到的 `<<<` 內容與是否來得及輸出 `???` 推進說明

## Test Plan
- 單元測試：
  - 驗證多行 payload 每一行都帶有正確前綴 `>>>`
  - 驗證多行 response 每一行都帶有正確前綴 `<<<`
  - 驗證 `???` 說明包含上一輪工具結果與下一輪目的
  - 驗證中文與特殊前綴在 UTF-8 下不亂碼
- 應用層測試：
  - 驗證單輪請求只出現 `>>>` 與 `<<<`，不多出 `???`
  - 驗證多輪工具呼叫請求會在每次再次送往 LLM 前出現 `???`
  - 驗證 follow-up payload 真的被當作新的 `>>>` 內容輸出
  - 驗證最終回覆以 `<<<` 顯示，與工具結果區分開來
- 手動驗收：
  - `(2*3 加 10)/8` 可看到完整的三輪 `>>> / <<< / ???` 互動
  - `23 加 58` 只需一輪工具呼叫時，應能看出是否仍有再次推論
  - 非四則運算輸入時，應能看到 `>>>` payload 與 `<<<` 拒答內容
  - `docs/agent-calculator-plan.md` 與 console 實際輸出都必須以 UTF-8 正常顯示

## Assumptions
- 這次工作的交付仍是更新既有 `docs/agent-calculator-plan.md`，不是新增另一份規格文件。
- `???` 代表系統層可觀測性說明，不主張它是模型的內部 chain-of-thought；它是根據當前工具結果與流程狀態生成的外部說明。
- `<<< TOOL CALL REQUEST` 與 `<<< FINAL RESPONSE` 都算收到的內容，因為它們都來自 LLM 的回傳。
- 若底層框架未直接暴露某次 follow-up payload 的完整原文，系統應記錄實際送出的 message 集合，以滿足 `>>>` 顯示需求。
