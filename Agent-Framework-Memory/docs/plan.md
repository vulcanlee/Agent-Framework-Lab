# .NET 10 Console 範例：完整逐輪紀錄 + 摘要記憶

## Summary
建立一個 `.NET 10` 的 Microsoft Agent Framework Console App，透過 GitHub Models `https://models.github.ai/inference` 與環境變數 `GITHUB_TOKEN` 執行多輪對話，並把記憶明確分成兩份檔案：

- `conversation-log.jsonl`：完整逐輪紀錄，保存每一輪 user / assistant 對話內容
- `memory-store.json`：摘要記憶，保存 Agent 整理過的重要資訊與主題摘要

`AgentSession` 也會持續落地到 `session.json`，負責讓同一個 session 能在程式重啟後接續。文件規劃寫入 `docs/`，讓這個範例能直接支撐技術文章。

## Key Changes
- 專案設定：
  - 建立單一 `.NET 10` Console App，目標框架固定為 `net10.0`
  - 使用 Microsoft Agent Framework 的 OpenAI provider，client 指向 GitHub Models inference endpoint
  - API Key 從 `GITHUB_TOKEN` 讀取，model 預設 `openai/gpt-4.1-mini`，允許 `GITHUB_MODEL` 覆蓋
- 記憶分層設計：
  - `session.json`
    - 保存 `AgentSession` 序列化結果
    - 作用是延續多輪上下文，不作為文章主角
  - `conversation-log.jsonl`
    - append-only
    - 每輪寫入一筆 JSON line
    - 欄位固定包含 `timestamp`、`sessionId`、`turn`、`userMessage`、`assistantMessage`
    - 這份檔案代表「完整逐輪紀錄」
  - `memory-store.json`
    - 保存整理後的長期摘要記憶
    - 欄位固定為 `profile`、`preferences`、`important_topics`、`open_loops`、`conversation_summary`
    - 這份檔案代表「摘要記憶」
- Agent 行為設計：
  - 主對話靠 `AgentSession` 維持上下文
  - 每輪結束後，先把完整對話 append 到 `conversation-log.jsonl`
  - 再用「舊摘要記憶 + 本回合對話」更新 `memory-store.json`
  - 不直接把整份完整對話紀錄重新餵回模型，避免 token 成本與 prompt 長度失控
- 摘要更新策略：
  - 使用單一、簡單、可寫文章的提示詞流程
  - 輸入：
    - 目前的 `memory-store.json`
    - 本回合的 `userMessage`
    - 本回合的 `assistantMessage`
  - 輸出：
    - 完整覆蓋式的新 `memory-store.json`
  - 摘要規則固定：
    - 只保留高價值資訊
    - 不逐字抄錄全部歷史
    - 不確定的資訊不要寫入事實欄位
    - `conversation_summary` 維持短小、可讀、適合文章展示

## Public Interfaces / Behavior
- 環境變數：
  - 必要：`GITHUB_TOKEN`
  - 可選：`GITHUB_MODEL`
- 命令列互動：
  - `:memory`
    - 顯示 `memory-store.json`
  - `:log`
    - 顯示最近幾筆 `conversation-log.jsonl`
  - `:reset`
    - 清除 `session.json`、`conversation-log.jsonl`、`memory-store.json`
    - 建立新的 session
  - `:exit`
    - 正常結束並保留目前狀態

## Test Plan
- 多輪記憶：
  - 輸入「我叫小明，我喜歡黑咖啡」
  - 再問「你記得我叫什麼嗎」
  - 預期回答正確，且摘要記憶出現姓名與偏好
- 檔案輸出：
  - 對話 3 到 5 輪後檢查 `conversation-log.jsonl`
  - 預期每輪都有一筆完整紀錄
  - 檢查 `memory-store.json`
  - 預期只有整理過的重點，不是全文轉錄
- 重啟持續性：
  - 關閉程式再重新啟動
  - 問「我們之前聊過什麼」
  - 預期能從 `session.json` 與 `memory-store.json` 延續
- 重設：
  - 執行 `:reset`
  - 再詢問舊資訊
  - 預期不再保留先前記憶
