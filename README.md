# Agent Framework Lab

這是一個以 `C#`、Microsoft Agent Framework、GitHub Models 與 OpenAI 相容工作流為核心的展示型實驗室，用來探索現代 AI Agent 在實務上可以如何被設計、組合與落地。

整個儲存庫由多個小型、聚焦的子專案組成。每個專案都專注驗證一種重要能力，例如記憶、技能、結構化輸出、工具呼叫、檔案理解、多 Agent 協作，以及工作流設計。

## 這個 Lab 在探索什麼

- `.NET` 與 `C#` 中的 Agent 設計模式
- Microsoft Agent Framework 的核心元件與組合方式
- GitHub Models 作為 OpenAI 相容模型後端的使用方式
- OpenAI Responses API 與 Code Interpreter 等 Hosted Tools
- Session Memory、Persistent Memory 與知識整理流程
- 結構化輸出、REPL 互動體驗與工作流自動化

## 專案亮點

### 記憶與知識管理

#### `Agent-Framework-Session-Memory`
展示以 Session 為範圍的對話記憶能力，適合用來理解 Agent 如何在同一段互動中保留短期上下文，並透過摘要與重置等指令管理狀態。

#### `Agent-Framework-Memory`
進一步示範對話紀錄、記憶儲存與記憶檢視機制，方便比較「完整對話歷史」與「萃取後可重用記憶」之間的差異。

#### `Agent-Framework-PersistenceMemory`
打造具備跨 Session 永續記憶的 PM 風格 Agent。它可以 ingest 需求內容、追蹤工作項目、管理來源資料，並將長對話沉澱成可持續使用的產品規劃資訊。

#### `Agent-Framework-LLM-Wiki`
探索如何從本機資料建立 LLM 驅動的 Wiki。這個專案可以匯入來源檔案、生成 Wiki 頁面、支援簡易問答，並透過 lint 流程維持知識庫品質。

### 技能與工具使用

#### `Agent-Framework-Skill`
一個精簡的 Skill 載入示範，展示 Agent 如何從磁碟上發現並載入本地 `SKILL.md`。內建範例聚焦在文章大綱與 release note 撰寫，很適合用來理解可重用提示能力的基本做法。

#### `Agent-Framework-Calculate`
展示 function calling 的基礎模式，讓 Agent 將數學需求路由到具體工具，如加減乘除，而不是只依賴自由生成文字回答。

#### `Agent-Framework-Skill-Code-Interpreter`
這是一個端到端 CLI，會收集 Hacker News、GitHub 與 Reddit 等來源中的 AI 話題訊號，再交由 Code Interpreter 進行分析。它是結合資料蒐集、Agent 協作與 Hosted Tool 能力的實用示範。

### 檔案與內容理解

#### `Agent-Framework-File-parse-1`
提供這個 Lab 中較基礎的檔案分析示範，重點在於如何把本機檔案內容納入 Agent 的推理流程，而不只是單純對話輸入。

#### `Agent-Framework-File-parse-2`
延伸檔案分析概念為更完整的多模態 CLI 體驗。它支援 prompt 搭配檔案輸入，能處理文字與圖片，示範本機檔案如何被整合進結構化的 Agent 請求。

#### `Agent-Framework-Structure-Output`
示範如何把非結構化會議內容轉成穩定的 JSON 與 Markdown 摘要。這個專案特別適合用來理解 schema-first 設計，也就是如何引導 Agent 輸出可預期、可程式化處理的結果。

### 工作流與多 Agent 協作

#### `Agent-Framework-Workflow`
以教學導向方式對照 Agent 模式與 Workflow 模式，幫助讀者理解什麼情境適合使用開放式 Agent loop，什麼情境更適合明確、可控的工作流管線。

#### `Agent-Framework-Multi-Agent-Workflow`
實作一條具備專責角色分工的 sequential multi-agent review flow，例如 parser、checker、reviewer 與 report writer。很適合拿來觀察複雜任務如何被拆解成多個較清楚的 Agent 職責。

#### `Agent-Framework-NVIDIA`
打造具備進度回報、schema normalization、網頁驗證與 REPL 互動的旅遊規劃 Agent。它呈現出更接近產品型 Agent 的樣貌，特別著重在外部資料整合與互動穩定性。

## 這些專案的價值

這個 Lab 想傳達的一件事是：`AI Agent` 並不是單一固定模式，而是一系列可依需求選擇的設計方法。在這個儲存庫中，你會看到多種不同型態的 Agent：

- 輕量型 CLI 助手
- REPL 式互動工具
- 具備階段分工的 Workflow Pipeline
- 可載入本地 Skill 的 Skill-aware Agent
- 能跨回合或跨 Session 持續保留上下文的 Memory-backed System
- 以本機檔案與知識組織為核心的文件理解工具

這種多樣性，讓這個儲存庫同時適合作為學習材料，也適合作為未來實作 Agent 系統時的參考索引。

## 共通技術主題

- 涵蓋 `.NET 8`、`.NET 9` 與 `.NET 10`
- 使用 Microsoft Agent Framework 進行 Agent 與 Workflow 組裝
- 透過 `https://models.github.ai/inference` 整合 GitHub Models
- 使用 OpenAI 相容的 Chat 與 Responses Client
- 以 Console 與 REPL 為主的互動式開發體驗
- 強調可觀察性，例如 log、trace、memory inspection 與結構化報告

## 適合哪些讀者

- 正在評估 Microsoft Agent Framework 的開發者
- 想用 `C#` 實作 Agent 模式的工程團隊
- 想比較 memory、workflow、skill 與 tool use 設計差異的工程師
- 想找小而清楚的參考實作，而不是大型單體範例的技術讀者

## 快速開始

這個 Lab 中的大多數專案彼此獨立，因此建議的閱讀與操作方式是：

1. 先挑選一個你想理解的能力主題，進入對應子專案資料夾。
2. 閱讀該子專案內的 `README.md` 與 `docs/` 文件。
3. 設定所需環境變數，最常見的是 `GITHUB_TOKEN`。
4. 使用 `dotnet run` 執行範例，或用 `dotnet test` 驗證測試。

多數專案預設使用 GitHub Models，也有部分情境會示範 OpenAI Hosted Capabilities。

## 建議探索順序

如果你第一次接觸這個 Lab，推薦依照以下順序閱讀：

1. `Agent-Framework-Workflow`
2. `Agent-Framework-Calculate`
3. `Agent-Framework-Structure-Output`
4. `Agent-Framework-Session-Memory`
5. `Agent-Framework-PersistenceMemory`
6. `Agent-Framework-Multi-Agent-Workflow`
7. `Agent-Framework-Skill-Code-Interpreter`

這條路徑會從較單純的互動模式，逐步進入更完整的記憶、協作與分析場景。
