# 極簡 PM Agent with Persistent Memory

這是一個用 `C#/.NET`、`Microsoft Agent Framework` 與 `GitHub Models` 實作的 console REPL 範例。Agent 扮演 PM，能把原始需求拆成工作項目、逐項檢討修正、輸出正式工作需求清單，並把跨 session 的資訊保存到 UTF-8 JSON。

## 主要功能

- `/ingest` 貼上原始需求
- `/ingest <text-file-path>` 直接從 UTF-8 文字檔讀入需求
- `/work review`、`/work update` 持續討論既有 work item
- `/work add` 手動新增 work item
- `/work remove` 刪除指定 work item
- `/source remove` 硬刪除整個 source
- `/finalize` 產出正式工作需求清單
- 每次與 LLM 互動後顯示 token 使用量
- 保存 `session memory` 與 `persistent memory`

## 可用指令

- `/help`
- `/ingest`
- `/ingest <text-file-path>`
- `/end`
- `/source remove <source-id>`
- `/work list [source-id]`
- `/work review <work-id>`
- `/work update <work-id> <修正內容>`
- `/work remove <work-id>`
- `/work add <title> --desc <description> [--owner <engineer>] [--accept <item1;item2>]`
- `/finalize <source-id> [--save <path>]`
- `/show-session`
- `/show-memory`
- `/new-session`

## 狀態訊息與 Token 使用量

處理需求時，REPL 會持續輸出 Agent 狀態與 usage：

```text
[Agent] 14:12:03 正在檢查目前 session 上下文
[Agent] 14:12:03 正在搜尋可能相關的永久記憶
[Agent] 14:12:04 Token 使用量：input=123, output=45, other=0
[Agent] 14:12:05 正在整理原始需求並拆解工作項目
[Agent] 14:12:06 Token 使用量：input=456, output=98, other=0
```

## 使用流程

```text
/ingest
會員管理後台需要角色權限管理，並提供審計紀錄查詢。
/end
/work review W1
請補上權限異動後的 email 通知
/work add 權限異動通知 --desc 加入 email 與站內通知 --owner Backend --accept 可設定通知內容;可關閉通知
/finalize source-001 --save docs/formal-requirements/source-001.md
```

## 範例

### 從文字檔匯入

```text
/ingest docs/sample-requirement.txt
```

### 刪除來源

```text
/source remove source-001
```

### 刪除工作項目

```text
/work remove W2
```

### 手動新增工作項目

```text
/work add 權限異動通知 --desc 加入 email 與站內通知 --owner Backend --accept 可設定通知內容;可關閉通知
```

### 找不到 work-id 也不會中止 REPL

```text
/work review w5
找不到工作項目 w5。
/work update w5 測試修正
找不到工作項目 w5。
```

## UTF-8 與 PowerShell 建議

```powershell
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$env:GITHUB_TOKEN="your_token"
dotnet run --project .\AgentFrameworkPersistenceMemory\AgentFrameworkPersistenceMemory.csproj
```

## 測試

```powershell
dotnet test .\AgentFrameworkPersistenceMemory.Tests\AgentFrameworkPersistenceMemory.Tests.csproj
```

## 文件

- [專案計畫](/C:/Vulcan/Projects/Agent-Framework-Lab/Agent-Framework-PersistenceMemory/docs/project-plan.md)
- [設計說明](/C:/Vulcan/Projects/Agent-Framework-Lab/Agent-Framework-PersistenceMemory/docs/design.md)
