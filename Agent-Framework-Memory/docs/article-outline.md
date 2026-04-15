# 技術文章大綱

## 文章標題候選
- 用 Microsoft Agent Framework 做出會記得上下文的 Console Agent
- 用 GitHub Models 實作 Agent 的記憶與持續性
- 如何把 Agent 對話拆成完整逐輪紀錄與摘要記憶

## 文章主軸
- 為什麼要分成完整逐輪紀錄與摘要記憶
- `AgentSession` 解決什麼問題
- `conversation-log.jsonl` 與 `memory-store.json` 各自扮演什麼角色

## 建議章節
1. 問題背景：為什麼 Agent 需要記憶與持續性
2. 架構總覽：`session.json`、`conversation-log.jsonl`、`memory-store.json`
3. 連接 GitHub Models：`GITHUB_TOKEN` 與 inference endpoint
4. 使用 Microsoft Agent Framework 建立多輪對話 Agent
5. 如何保存完整逐輪紀錄
6. 如何把逐輪對話整理成摘要記憶
7. 指令示範：`:memory`、`:log`、`:reset`
8. 實際測試：重啟程式後如何延續先前對話
9. 這種簡化設計的優點與限制

## 重點圖表建議
- 一張流程圖：使用者輸入 -> Agent 回答 -> 寫入 conversation log -> 更新 memory store -> 保存 session
- 一張比較表：完整逐輪紀錄 vs 摘要記憶
