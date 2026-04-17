# AI Topic Pulse CLI 實作計畫

這份文件必須與 `README.md` 保持同步，作為本專案後續異動的設計基準。

## 目標

建立一個簡單、真實、可拿來撰寫技術文章的 C# 範例，示範如何：

- 依照 Microsoft Agent Framework 的 OpenAI provider 文件設計整體流程
- 使用 OpenAI API
- 啟用 `Code Interpreter`
- 從多個資料來源收集 AI 討論
- 在螢幕上即時顯示每個 agent 的狀態

## 實作範圍

第一版包含：

- CLI 專案
- 固定 AI 主題
- Hacker News / GitHub / Reddit 三個來源
- 統一資料模型
- OpenAI Responses + Code Interpreter 分析
- 即時狀態訊息
- 終端機報告輸出
- 單元測試

第一版不包含：

- Web API
- 自訂查詢參數
- Markdown / CSV 檔案輸出
- X 平台整合

## 明確的責任切分

### C# 應用程式負責

- 載入 `OpenAI_Key`
- 呼叫外部資料來源
- 統一資料欄位
- 建立 JSON dataset
- 管理部分成功策略
- 顯示來源 agent 與分析 agent 的狀態訊息

### Code Interpreter 負責

- 讀取 JSON dataset
- 用 Python 做資料分析
- 去重
- 排名
- 產出 Top 10
- 生成繁體中文總結
- 在串流過程中回報分析狀態

## 架構摘要

### 設定

- `AppOptions`
- `AppOptionsLoader`

### 資料來源

- `ITrendingSource`
- `HackerNewsSource`
- `GitHubSource`
- `RedditSource`
- `TopicMention`
- `SourceFetchResult`

### 分析

- `AnalysisDataset`
- `AnalysisDatasetBuilder`
- `CodeInterpreterPromptFactory`
- `OpenAiCodeInterpreterAnalyzer`
- `ResponseUpdateStatusFormatter`
- `PulseReport`

### 狀態輸出

- `IStatusReporter`
- `ConsoleStatusReporter`
- `AiTopicPulseWorkflow`

### 最終輸出

- `ConsoleReportWriter`

## 螢幕狀態訊息設計

狀態訊息分成兩層：

### Workflow 層

負責顯示：

- 載入設定
- 每個來源 agent 開始抓取
- 每個來源 agent 成功或失敗
- 建立分析資料集
- 開始分析
- 分析完成

### Code Interpreter 串流層

負責顯示：

- Analysis request created
- Model is processing the request
- Code Interpreter started
- Code Interpreter is analyzing the dataset
- Code Interpreter finished running
- Model response completed

## Agent Framework 與 Code Interpreter 的落點

本專案參考 Microsoft Agent Framework 的 OpenAI provider 與 Code Interpreter 文件來設計。  
目前 C# 版 `Microsoft.Agents.AI.OpenAI` 在 `AsAIAgent(...)` 與 OpenAI Responses hosted `Code Interpreter` tool 的型別接合上，無法直接像概念範例那樣一路串到底。因此：

- 專案仍採用 Agent Framework 文件中的 OpenAI provider 思路
- 真正啟用 `Code Interpreter` 的分析步驟，使用 OpenAI .NET Responses SDK

這樣的設計目的是兼顧：

- 範例可執行
- 結構簡單
- 技術文章可誠實說明現況

## 測試策略

目前測試覆蓋：

- `OpenAI_Key` 讀取成功 / 失敗
- 分析資料集只接受成功來源
- 全部來源失敗時應中止
- Prompt 內容明確要求使用 Code Interpreter
- Workflow 會依序回報來源與分析狀態
- Responses 串流更新會被翻成可讀狀態訊息

後續若功能擴充，`README.md` 與本文件都必須同步更新，並保持 UTF-8 編碼。
