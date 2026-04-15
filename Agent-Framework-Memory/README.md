# Agent Memory Demo

一個使用 `.NET 10`、Microsoft Agent Framework 與 GitHub Models 建立的最小 Console 範例，示範 Agent 如何做到：

- 多輪對話中的上下文延續
- 對話結束後的本機持續化
- 將完整對話紀錄與摘要記憶分開保存

這個範例刻意採用簡單設計，方便拿來撰寫技術文章或做教學示範。

## 功能重點

- 使用 `GITHUB_TOKEN` 連接 GitHub Models inference endpoint
- 使用 `AgentSession` 保存多輪對話狀態
- 將每一輪完整對話 append 到 `conversation-log.jsonl`
- 將整理後的重點記憶保存到 `memory-store.json`
- 重新啟動程式後，仍可延續先前對話內容

## 專案結構

```text
.
├─ AgentMemoryDemo/
│  ├─ Program.cs
│  ├─ AgentMemoryDemo.csproj
│  ├─ session.json
│  ├─ conversation-log.jsonl
│  └─ memory-store.json
├─ docs/
│  ├─ plan.md
│  └─ article-outline.md
└─ README.md
```

說明：

- `session.json`
  - 保存 `AgentSession`
  - 用來延續 Agent 的對話 session
- `conversation-log.jsonl`
  - 完整逐輪紀錄
  - 一行一筆 JSON，不覆寫舊資料
- `memory-store.json`
  - 摘要記憶
  - 保存使用者資訊、偏好、重要主題、未完成事項與摘要

## 環境需求

- `.NET 10 SDK`
- 一組可用的 GitHub Models Token

必要環境變數：

```powershell
$env:GITHUB_TOKEN = "your_token_here"
```

可選環境變數：

```powershell
$env:GITHUB_MODEL = "openai/gpt-4.1-mini"
```

若未設定 `GITHUB_MODEL`，程式會預設使用 `openai/gpt-4.1-mini`。

## 執行方式

先切換到專案目錄：

```powershell
cd .\AgentMemoryDemo
```

執行：

```powershell
dotnet run
```

## 互動指令

程式啟動後可使用以下特殊指令：

- `:memory`
  - 顯示目前的摘要記憶內容
- `:log`
  - 顯示最近幾筆完整逐輪對話紀錄
- `:reset`
  - 清除 `session.json`、`conversation-log.jsonl`、`memory-store.json`
- `:exit`
  - 正常結束程式

## 示範流程

你可以直接用這組測試：

```text
My name is Alice and I like black coffee.
What is my name?
:memory
:log
:exit
```

第一次執行時，Agent 會記住：

- 你的名字是 Alice
- 你喜歡 black coffee

此時：

- `conversation-log.jsonl` 會保存完整逐輪對話
- `memory-store.json` 會保存整理後的摘要記憶
- `session.json` 會保存 session 狀態

之後重新啟動程式，再輸入：

```text
What did we talk about before?
```

Agent 應該可以回答先前談到的內容。

## 記憶分層設計

這個範例的核心概念是把記憶拆成兩份主資料，加上一份 session 狀態：

### 1. 完整逐輪紀錄

檔案：`conversation-log.jsonl`

用途：

- 保存每一輪的 user / assistant 對話
- 適合追蹤歷史、除錯與文章展示

欄位範例：

```json
{
  "Timestamp": "2026-04-15T02:16:53.4669618+00:00",
  "SessionId": "local-session",
  "Turn": 1,
  "UserMessage": "My name is Alice and I like black coffee.",
  "AssistantMessage": "Hi Alice! Nice to meet you. Black coffee is a great choice."
}
```

### 2. 摘要記憶

檔案：`memory-store.json`

用途：

- 保存高價值資訊
- 避免每一輪都把全部歷史重新塞回 prompt
- 讓 Agent 能快速回顧之前聊過的重點

欄位範例：

```json
{
  "Profile": {
    "Name": "Alice",
    "Bio": null
  },
  "Preferences": [
    "black coffee"
  ],
  "ImportantTopics": [
    "preference for black coffee"
  ],
  "OpenLoops": [
    "how Alice usually enjoys her coffee"
  ],
  "ConversationSummary": "User is Alice who prefers black coffee. Discussion included an open question about how she usually enjoys her coffee."
}
```

### 3. Session 狀態

檔案：`session.json`

用途：

- 保存 Agent Framework 的 `AgentSession`
- 讓同一個 session 可在程式重啟後恢復

## 實作思路

主程式做的事情很單純：

1. 從環境變數讀取 `GITHUB_TOKEN`
2. 建立指向 `https://models.github.ai/inference` 的 OpenAI client
3. 還原 `session.json`
4. 載入 `memory-store.json`
5. 進入 Console REPL
6. 每輪回應後：
   - append 完整對話到 `conversation-log.jsonl`
   - 更新 `memory-store.json`
   - 保存 `session.json`

其中摘要記憶更新採用「另一個 memory summarizer agent」來整理本回合資訊，讓主 Agent 專注在對話本身。

## 驗證方式

建置：

```powershell
dotnet build
```

建議至少驗證以下情境：

1. 同一次執行內，Agent 可以記住使用者剛剛說過的名字
2. `conversation-log.jsonl` 會逐輪新增紀錄
3. `memory-store.json` 只保留重點，不是全文轉錄
4. 重啟程式後，Agent 仍可回顧先前聊過的內容
5. `:reset` 後，先前記憶會被清除

## 文件

- `docs/plan.md`
  - 實作計畫
- `docs/article-outline.md`
  - 技術文章大綱

## 備註

這是一個偏教學用途的最小範例，因此沒有引入：

- 資料庫
- 向量搜尋
- RAG
- 多 Agent 協作
- Web API 介面

如果後續想擴充，可以再往下加：

- SQLite 或 PostgreSQL 持久化
- 向量化記憶搜尋
- Web UI / API
- 更細緻的記憶分類策略
