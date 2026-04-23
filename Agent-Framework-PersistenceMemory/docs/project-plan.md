# 專案計畫

## 目標

把 PM Agent REPL 擴充成更完整的需求分析工作台，包含：

- source 的硬刪除
- work item 的手動新增與刪除
- 檔案 ingest
- 每次 LLM 呼叫後顯示 token 使用量
- 自然語言聊天模式

## 本輪範圍

- `/source remove <source-id>`
- `/work remove <work-id>`
- `/work add <title> --desc <description> [--owner <engineer>] [--accept <item1;item2>]`
- `/ingest <text-file-path>`
- 聊天模式：非 slash command 預設進入 discuss
- GitHub Models usage 顯示
- README / docs 同步更新

## 驗收重點

- 刪除 active source 時，session 狀態會同步清空
- work item 手動新增後可以被 finalize 輸出
- work item 刪除後不重排編號
- 檔案 ingest 可正確讀取 UTF-8 中文
- 每次 LLM 呼叫後都顯示 `Token 使用量：input=..., output=..., other=...`
- `work-id` 或 `source-id` 找不到時，只顯示友善訊息，不中止 REPL
- 非 slash command 的輸入會進入聊天模式，而且不會直接改資料
