# 設計說明

## 核心目標

這個專案要示範一個簡單、容易寫成技術文章的 PM Agent：

- 用 REPL 接收原始需求
- 拆成可討論的 `work item`
- 允許逐項檢討與修正
- 最後輸出正式工作需求清單
- 用永久記憶保存跨 session 的需求背景

## 主要元件

### `PmConsoleApp`

- REPL 入口
- 負責 slash command 解析
- 處理 ingest 貼上模式
- 攔截可恢復的 user-facing 錯誤，避免整個程式退出

### `PmWorkflow`

- 串接 session、memory recall、Agent 分析與記憶保存
- 負責 source ingest、work item revise、finalize
- 輸出每個主要步驟的狀態訊息

### `AgentFrameworkPmAgentService`

- 用 `Microsoft.Agents.AI.Abstractions` 的 agent/session 包裝 GitHub Models 呼叫
- 提供三種分析入口：
  - ingest source
  - revise work item
  - finalize source
- 內建「忠於原始需求」的 prompt 規則與偏題 fallback

### `PersistentMemoryStore`

- 以 UTF-8 JSON 讀寫永久記憶
- 保存 `source`、`work item`、`formalized output`

### `SessionManager`

- 保存短期上下文
- 管理 active source、active work item、ingest buffer

## 忠實度保護

這一版特別處理模型偏題問題：

- prompt 強制要求只能根據原始材料與既有 work item 回答
- 先抽取 source keywords，再檢查模型輸出是否仍保留這些關鍵詞
- 若輸出偏離原始主題，ingest 會退回成「需要重新確認的需求」
- finalize 若偏題，會改用本地保存的 work item 生成保守版正式清單

## UTF-8 策略

- 程式啟動時設定 `Console.InputEncoding`、`Console.OutputEncoding` 為 UTF-8
- 輸入會做 BOM 去除與 `NormalizationForm.FormC` 正規化
- JSON 與正式輸出檔案都用 UTF-8 寫入
- README 與 docs 同步維持 UTF-8

## 非致命錯誤處理

`work-id` 或 `source-id` 找不到時，屬於互動錯誤，不是系統啟動錯誤。

預期行為：

```text
/work review w5
找不到工作項目 w5。

/work update w5 測試修正
找不到工作項目 w5。
```

REPL 在這種情況下要繼續可用，不能掉到外層 `啟動失敗`。

## 狀態訊息

為了讓技術文章讀者看懂 Agent 內部流程，每次關鍵步驟都會輸出狀態，例如：

- 正在檢查目前 session 上下文
- 正在搜尋可能相關的永久記憶
- 正在判斷哪些歷史需求與本次問題相關
- 正在整理原始需求並拆解工作項目
- 正在檢討並修正工作項目 W1
- 正在彙整正式工作需求清單
- 正在寫回永久記憶
