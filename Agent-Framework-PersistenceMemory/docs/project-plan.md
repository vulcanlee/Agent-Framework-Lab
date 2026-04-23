# 專案計畫

## 目標

讓這個 PM Agent 範例具備更完整的 REPL 管理能力：

- 刪除整個 source
- 刪除 work item
- 手動新增 work item
- 從文字檔 ingest
- 顯示每次 LLM 互動的 token 使用量

## 已實作能力

- `/source remove <source-id>`
- `/work remove <work-id>`
- `/work add <title> --desc <description> [--owner <engineer>] [--accept <item1;item2>]`
- `/ingest <text-file-path>`
- GitHub Models usage 解析與顯示
- README / docs 同步更新

## 驗證重點

- 刪除 active source 後 session 狀態會一起清掉
- work item 手動新增後會出現在 finalize 結果
- work item 刪除不會重排編號
- 文字檔 ingest 能正確讀取 UTF-8 中文
- 每次 LLM 呼叫後都會看到 `Token 使用量：input=..., output=..., other=...`
- `work-id` 或 `source-id` 找不到時，只回報錯誤，不中止 REPL
