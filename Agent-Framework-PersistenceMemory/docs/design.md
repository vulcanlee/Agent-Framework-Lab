# 設計說明

## 核心目標

這個專案示範一個極簡 PM Agent：

- 以 REPL 接收需求
- 拆成 `source + work item`
- 支援持續檢討、手動新增、刪除
- 支援自然語言聊天分析
- 輸出正式工作需求清單
- 保存跨 session 的永久記憶

## 主要元件

### `PmConsoleApp`

- 解析 slash command
- 管理 ingest 貼上模式與檔案模式
- 非 slash command 時自動進入聊天模式
- 統一攔截 user-facing 錯誤

### `PmWorkflow`

- 串接 session、memory recall、Agent 分析與持久化
- 管理 source ingest、work item review/update/add/remove、source remove、finalize
- 在聊天模式下整理 active source、active work item 與 recalled memories

### `AgentFrameworkPmAgentService`

- 使用 Microsoft Agent Framework 封裝 PM Agent
- 提供結構化的 ingest、review、finalize 與 discuss 能力
- 每次呼叫 LLM 後回報 token usage

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

## 互動模式

### Slash command

適合做明確資料操作，例如：

- `/ingest`
- `/source remove <source-id>`
- `/work add ...`
- `/work remove <work-id>`
- `/finalize <source-id>`

### 自然語言聊天

只要輸入不是 slash command，就會進入聊天模式。聊天模式會：

- 優先帶入 active source
- 若有 active work item，也會把該項內容一起帶入
- 允許使用者詢問驗收條件、風險、優先順序與拆解建議
- 不會直接修改資料或寫回 persistent memory

## 刪除與新增

### source 硬刪除

- `/source remove <source-id>`
- 直接從 persistent memory 移除整筆 source
- 若刪掉的是 active source，會同步清掉 active source / active work item

### work item 手動新增與刪除

- `/work add ...` 直接加入目前 active source
- `/work remove <work-id>` 只刪除指定 work item
- work item 編號不重排，避免討論紀錄失焦

## 檔案 ingest

- `/ingest <path>` 以 UTF-8 讀取文字檔
- 與貼上模式共用同一條 ingest workflow
- 讀取失敗時只回報錯誤，不中止 REPL

## Token usage 顯示

- recall relevance 若有呼叫 LLM，也會顯示 usage
- ingest / review / finalize / discuss 都會顯示 usage
- `other` 優先使用 API 回傳的其他 usage 欄位
- 若 API 只給 `total`，則使用 `total - input - output`
- 若無法取得，預設顯示 `0`
