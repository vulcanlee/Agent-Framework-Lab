# 極簡 PM Agent with Persistent Memory

這是一個用 `C#/.NET`、`Microsoft Agent Framework` 與 `GitHub Models` 實作的 console REPL 範例。Agent 扮演 PM，能把原始需求拆解成工作項目、支援逐項檢討修正，並在最後輸出正式工作需求清單。專案同時示範 `session memory` 與 `persistent memory` 的分工，方便拿來寫技術文章或教學。

## 這個 Agent 可以做什麼

- 用 `/ingest` 匯入一段長篇原始需求
- 自動拆解成多個 `work item`
- 用 `/work review`、`/work update` 或自然語言繼續檢討工作項目
- 用 `/finalize` 產出正式工作需求清單
- 把 `source`、`work item` 與正式輸出保存到 UTF-8 JSON 永久記憶
- 每次處理時顯示 Agent 正在做什麼，方便觀察內部流程

## 可用指令

- `/help`
  - 顯示所有功能與簡例
- `/new-session`
  - 建立新的空白 session
- `/show-session`
  - 顯示目前 session 摘要
- `/show-memory`
  - 顯示永久記憶中的來源列表
- `/ingest`
  - 進入貼上模式
- `/end`
  - 結束貼上模式並開始拆解工作項目
- `/work list [source-id]`
  - 列出指定來源的工作項目
- `/work review <work-id>`
  - 檢視指定工作項目內容
- `/work update <work-id> <修正內容>`
  - 直接修正指定工作項目
- `/finalize <source-id> [--save <path>]`
  - 產出正式工作需求清單，並可另存檔案

## Agent 執行時會顯示的狀態訊息

處理需求時，REPL 會持續輸出類似下面的訊息：

```text
[Agent] 14:12:03 正在檢查目前 session 上下文
[Agent] 14:12:03 正在搜尋可能相關的永久記憶
[Agent] 14:12:04 正在判斷哪些歷史需求與本次問題相關
[Agent] 14:12:05 正在整理原始需求並拆解工作項目
[Agent] 14:12:06 正在寫回永久記憶
[Agent] 14:12:10 正在檢討並修正工作項目 W1
[Agent] 14:12:14 正在彙整正式工作需求清單
```

## 記憶設計

### Session Memory

- 只屬於目前這次 REPL 討論
- 保存 active source、active work item、ingest buffer、Agent 筆記
- 執行 `/new-session` 後會清空

### Persistent Memory

- 保存 `source`、`work item`、`formalized output`
- 寫入 `data/persistent-memory.json`
- 全程使用 UTF-8 儲存
- 新 session 如果談到舊需求，可以從這裡回填相關上下文

## 忠於原始需求的設計

這個版本特別加強了三件事：

- prompt 明確要求 Agent 只能根據原始材料與既有 work item 回答
- 若模型輸出與原始需求關鍵詞明顯不一致，會回退成「需要重新確認的需求」
- `finalize` 階段若結果偏題，會改用本地保存的 work item 組出保守版正式清單

## UTF-8 與 PowerShell 建議

程式啟動時會設定 `Console.InputEncoding` 與 `Console.OutputEncoding` 為 UTF-8，但在 Windows PowerShell 測試時，仍建議先確認主控台編碼一致：

```powershell
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$env:GITHUB_TOKEN="your_token"
dotnet run --project .\AgentFrameworkPersistenceMemory\AgentFrameworkPersistenceMemory.csproj
```

## 使用流程

```text
/ingest
貼上原始需求內容...
/end
/work list source-001
/work review W1
請補上權限異動後的 email 通知
/finalize source-001 --save docs/formal-requirements/source-001.md
```

## 範例

### 範例 1：全新需求拆解

```text
/ingest
會員管理後台需要角色權限管理，並提供審計紀錄查詢。
/end
```

Agent 會建立一個新的 `source`，並拆出像是「會員角色權限」與「審計紀錄查詢」這類工作項目。

### 範例 2：檢討單一工作項目

```text
/work review W1
請補上權限異動後的 email 通知
```

如果目前有 active source 與 active work item，直接輸入自然語言也會視為針對該工作項目的修正建議。

### 範例 3：直接更新 work item

```text
/work update W2 加入審計紀錄的時間區間與操作人篩選
```

這會直接把修正內容交給 Agent 重新整理成較正式的工作描述與驗收條件。

### 範例 4：找不到 work-id 也不會中止 REPL

```text
/work review w5
找不到工作項目 w5。
/work update w5 測試修正
找不到工作項目 w5。
```

這類錯誤屬於可恢復互動錯誤，不會讓程式出現 `啟動失敗` 或直接退出。

### 範例 5：輸出正式工作需求清單

```text
/finalize source-001 --save docs/formal-requirements/source-001.md
```

正式清單至少會包含：

- 需求來源摘要
- 全部正式工作項目
- 每項工作的最終說明
- 驗收重點
- 指派建議

## 測試

```powershell
dotnet test .\AgentFrameworkPersistenceMemory.Tests\AgentFrameworkPersistenceMemory.Tests.csproj
```

目前測試覆蓋：

- UTF-8 JSON 持久化
- `/help` 與 BOM 輸入
- `/ingest` 到 `/end` 的流程
- `/work review`、`/work update` 找不到 work-id 時不會中止程式
- workflow 的 revise / finalize 流程
- 模型偏題時的 fallback 行為

## 文件

- [專案計畫](/C:/Vulcan/Projects/Agent-Framework-Lab/Agent-Framework-PersistenceMemory/docs/project-plan.md)
- [設計說明](/C:/Vulcan/Projects/Agent-Framework-Lab/Agent-Framework-PersistenceMemory/docs/design.md)

## 參考資料

- [Microsoft Agent Framework 文件](https://learn.microsoft.com/zh-tw/agent-framework/overview/?pivots=programming-language-csharp)
- [GitHub Models inference API](https://docs.github.com/en/rest/models/inference)
- [GitHub Models Quickstart](https://docs.github.com/en/github-models/quickstart)
