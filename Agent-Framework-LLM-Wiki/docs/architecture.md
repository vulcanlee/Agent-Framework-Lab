# 架構說明

這個專案把整體流程切成三層，目標是保持最小可行與易於教學。

## CLI 層

`src/WikiCli/Program.cs` 負責：

- 解析指令
- 載入環境變數
- 建立服務與 agent
- 呼叫 `WikiApplication`

## 應用服務層

`src/WikiCli/Services/WikiServices.cs` 內包含：

- `WikiApplication`
- `WikiFileService`
- `SourceTextExtractor`
- `WikiSearchService`

## Agent 與模型層

`src/WikiCli/AI/` 內包含：

- `GitHubModelsChatClient`
- `WikiAgent`

## 流程摘要

### Ingest

1. 複製來源到 `raw/imported/`
2. 轉成純文字
3. 搜尋既有 wiki 頁面
4. 讓 Agent 產生 source page 與 topic/entity page
5. 重建 index，追加 log

### Ask

1. 搜尋 `wiki/`
2. 把 index 與候選頁交給 Agent
3. 要求只根據 wiki 回答並附引用

### Lint

1. 本地規則找孤兒頁、重複標題、缺少 `Last updated` 的頁面
2. 讓 Agent 生成 markdown 報告
3. 寫入 `wiki/analyses/`
