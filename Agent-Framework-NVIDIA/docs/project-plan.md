# 專案計畫

## 目標

這個專案的目標是建立一個容易說明、方便撰寫技術文章的旅遊規劃 Agent 範例。它採用 `C# Console` 與 Microsoft Agent Framework，並把旅遊需求分析、網路搜尋、來源驗證、行程生成與畫面進度顯示都放在同一個簡潔的 console 專案中。

## 目前架構

主流程由 [TravelPlannerEngine.cs](/C:/Vulcan/Projects/Agent-Framework-Lab/Agent-Framework-NVIDIA/NvidiaTravelAgent/Services/TravelPlannerEngine.cs) 負責：

1. 分析旅遊需求
2. 判斷策略顯示訊息
3. 建立搜尋主題
4. 搜尋候選來源
5. 驗證網頁資訊
6. 用已驗證資料生成行程
7. 輸出旅遊行程建議清單

Agent 入口在 [TravelPlannerAgent.cs](/C:/Vulcan/Projects/Agent-Framework-Lab/Agent-Framework-NVIDIA/NvidiaTravelAgent/Agents/TravelPlannerAgent.cs)，CLI / REPL 入口在 [Program.cs](/C:/Vulcan/Projects/Agent-Framework-Lab/Agent-Framework-NVIDIA/NvidiaTravelAgent/Program.cs)。

## 進度事件機制

專案已加入統一的 progress reporter，相關型別位於 [Progress](/C:/Vulcan/Projects/Agent-Framework-Lab/Agent-Framework-NVIDIA/NvidiaTravelAgent/Progress)：

- `IProgressReporter`
- `ProgressStage`
- `ProgressDetailLevel`
- `ProgressUpdate`
- `NullProgressReporter`

目前使用的階段如下：

- `AnalyzingPrompt`
- `ChoosingStrategy`
- `ClarifyingMissingInfo`
- `PlanningResearch`
- `SearchingWeb`
- `VerifyingSources`
- `SynthesizingItinerary`
- `FormattingChecklist`
- `Completed`
- `Failed`

CLI / REPL 會透過 `ConsoleProgressReporter` 將這些事件顯示為：

```text
[進度] 正在分析旅遊需求...
```

## 設計原則

- 進度訊息只描述目前正在做什麼，不混入最終行程內容
- 最終輸出固定是一份 Markdown 旅遊行程建議清單
- 網頁資訊必須先驗證，才能進入最終輸出
- 模型輸出格式不穩時，會先做 normalization 並自動重試一次
- REPL 不應因單次模型輸出失敗而直接崩潰

## 目前限制

- 策略判斷目前主要用於顯示進度，尚未完整實作真正的多輪內部規劃
- 搜尋與驗證流程仍是簡化版，偏向技術示範
- 不處理訂位、付款與即時價格保證

## 後續擴充方向

- 將策略判斷升級成真正的 `Direct / Iterative / Clarify` 執行分支
- 強化餐飲、蛋塔、住宿區域等研究任務導向的查詢規劃
- 把最終輸出細分為餐飲推薦、蛋塔購買清單、交通動線與待確認事項
- 視需要加入更完整的 provider abstraction，降低對單一模型端點的耦合
