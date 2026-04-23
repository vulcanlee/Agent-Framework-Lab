# 設計說明

## 核心目標

這個專案示範一個極簡 PM Agent：

- 以 REPL 接收需求
- 拆成 `source + work item`
- 支援持續檢討、手動新增、刪除
- 輸出正式工作需求清單
- 保存跨 session 的永久記憶

## 主要元件

### `PmConsoleApp`

- slash command 解析
- ingest 貼上模式與檔案模式
- `/source remove`
- `/work add`、`/work remove`
- user-facing 錯誤攔截

### `PmWorkflow`

- 串接 session、memory recall、Agent 分析與持久化
- 管理 source ingest、work item revise、work item add/remove、source remove、finalize

### `GitHubModelsClient`

- 呼叫 GitHub Models chat completions
- 回傳內容與 usage
- 將 usage 轉成 `input / output / other`

### `AgentStatusReporter`

- 顯示工作流程狀態
- 顯示每次 LLM 互動的 token 使用量

## 記憶模型

### `source`

- 原始需求全文
- 標題與摘要
- keywords / decisions / tasks / assignments
- work items
- finalized output

### `work item`

- 穩定編號，例如 `W1`
- original / current / finalized description
- discussion notes / revision suggestions
- acceptance criteria
- suggested engineer
- status

## 新增能力

### source 硬刪除

- `/source remove <source-id>`
- 直接從永久記憶移除整筆 source
- 若正好是 active source，會同步清掉 active source / active work item

### work item 手動新增與刪除

- `/work add ...` 直接寫入目前 active source
- `/work remove <work-id>` 只刪除單一 work item
- 編號使用下一個可用號碼，不重排現有編號

### 檔案 ingest

- `/ingest <path>` 直接從 UTF-8 檔案讀入
- 與貼上模式共用同一條 ingest workflow

### Token usage 顯示

- recall relevance 若走 LLM，會顯示一次 usage
- ingest / revise / finalize 各自顯示一次 usage
- `other` 優先取 API 其他 usage 欄位，否則用 `total - input - output`，再不行就顯示 `0`
