# 專案計畫

## 背景

這個範例專案的目的，是示範如何用 Microsoft Agent Framework 建立一條簡單、清楚、適合技術文章說明的多 Agent 審查流程。情境聚焦在圖面初審、尺寸檢查與公差規則檢查。

## 為什麼選 CLI

- CLI 啟動成本最低，最適合教學。
- 使用者可以直接看到輸入到輸出的完整流程。
- 不需要先處理 API 路由、前端介面或部署議題。

## 為什麼選 Sequential Workflow

- 這個場景天然就是前一步輸出給下一步的鏈式審查。
- Feature parsing、尺寸檢查、公差檢查、報告彙整的責任邊界清楚。
- 適合展示 Agent Framework 的基本編排能力，而不會因為平行節點或 handoff 增加理解成本。

## 實作重點

- 透過 `WorkflowBuilder` 與 `AddChain` 建立一條順序式 workflow。
- 每個 executor 僅負責單一職責，方便文章逐段拆解。
- 模型呼叫層集中到 `GitHubModelsClient`，避免 workflow 程式混入太多 HTTP 細節。
- 前三個 Agent 強制輸出結構化 JSON，最後一個 Agent 產出 Markdown 報告。

## 不做的事情

- 不納入資料庫或持久化審查歷程。
- 不建立 Web API 或前端。
- 不嘗試實作完整 CAD feature graph 引擎。
- 不加入複雜的公司規範管理介面。

## 預期成果

- 一個可以 `dotnet run` 的最小專案。
- 一份 sample input。
- 一份 Markdown 審查報告。
- 一組可直接引用到技術文章的設計與流程文件。
