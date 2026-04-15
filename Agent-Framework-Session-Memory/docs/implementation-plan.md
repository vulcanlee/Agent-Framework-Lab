# Microsoft Agent Framework + GitHub Models 多回合記憶範例計畫

## 摘要

- 建立一個最小可執行的 `.NET` 主控台 REPL 範例，示範如何用 Microsoft Agent Framework 做多回合對話與 session 內記憶。
- 記憶不做任何持久化；程式重啟後對話記憶清空。
- GitHub Models 採 OpenAI 相容端點方式整合，使用 `GITHUB_TOKEN` 環境變數，端點為 `https://models.github.ai/inference`。

## 實作重點

- 專案採單一主控台應用程式設計，減少範例複雜度。
- 使用單一 agent，不加入工具、工作流或外部儲存。
- 多回合狀態只靠 `AgentSession` 維持，不額外實作自訂記憶 provider。
- 預設模型為 `openai/gpt-4.1`，可透過 `GITHUB_MODEL` 覆寫。
- 提供三個指令：
  - `/summary`
  - `/reset`
  - `/exit`

## 驗收標準

- 未設定 `GITHUB_TOKEN` 時，程式會在啟動時顯示明確錯誤。
- 已設定 `GITHUB_TOKEN` 時，可正常進入 REPL。
- 在同一個 session 中，多輪對話可引用先前內容。
- 執行 `/summary` 時，Agent 可整理前文。
- 執行 `/reset` 或重啟程式後，不再保留先前記憶。

## 文件產出

- `README.md`：面向讀者的教學文件。
- `docs/implementation-plan.md`：保存這份計畫內容，供後續技術文章撰寫使用。
