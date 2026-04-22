# 專案計畫

## 專案目標

建立一個簡潔、可執行、可寫成技術文章的旅遊規劃 Agent 範例。專案使用 Microsoft Agent Framework 作為 Agent 抽象層，並以 NVIDIA 免費模型資源作為主要 LLM。

使用者可以透過：

- 無參數直接進入 `repl`
- `plan`：一次輸入需求，取得完整旅遊行程
- `repl`：明確指定連續對話模式

## 技術決策

### 1. 採用 C# Console

這個專案刻意選擇 `C# Console`，因為：

- 與 Microsoft Agent Framework 文件脈絡一致
- 程式入口清楚，適合文章示範
- 比 Web UI 更容易聚焦在 Agent 流程本身

### 2. 採用自訂 `AIAgent`

本專案沒有強行把搜尋做成模型原生 tool-calling，而是採用更容易理解的方式：

- `TravelPlannerAgent : AIAgent`
- Agent 內部委派給 `TravelPlannerEngine`
- Engine 以程式碼方式協調搜尋、驗證、生成與輸出

### 3. JSON 容錯與自動重試

因為模型回傳 JSON 的形狀不一定完全穩定，解析層加入了兩個保護：

- `ModelJsonNormalizer`：先把常見 shape mismatch 修正成目標 schema
- `NvidiaChatClient` 一次自動重試：第一次解析失敗時，回傳明確 schema 再請模型重送

目前會容忍的情況包含：

- `specialRequirements` 為單一字串、物件、混合陣列
- `transportationNotes` / `accommodationNotes` / `cautions` 為單一字串
- `dailyPlans[*].items` 缺失或為 `null`

對缺少必要欄位如 `destination`、`days`、`travelStyle`，仍視為失敗，但會回傳應用層友善錯誤，而不是直接丟出 `JsonException`。

## 啟動與互動設計

CLI 行為固定如下：

- 無參數：直接進入 REPL
- `help` / `--help` / `-h`：顯示用法
- `repl`：明確指定 REPL
- `plan --request "..."`：輸出單次行程

REPL 啟動時會先設定 `Console.InputEncoding` 與 `Console.OutputEncoding` 為 UTF-8。

若模型輸出格式仍然不合規，REPL 只會顯示友善錯誤訊息，會話本身會繼續存活，使用者可以直接輸入下一輪需求。

若程式偵測到輸入流直接回傳 EOF/null，代表目前不是互動式標準輸入環境；此時會顯示提示訊息並結束，避免看起來像程式異常秒退。

## 測試策略

本專案目前採用 xUnit，測試分成四層：

### 單元測試

- 環境變數讀取
- 需求模型 JSON 解析
- 搜尋結果清理
- HTML 驗證萃取
- 行程來源驗證

### JSON 容錯測試

- `specialRequirements` 字串轉陣列
- `specialRequirements` 物件壓平成字串陣列
- `transportationNotes` / `accommodationNotes` / `cautions` 字串轉陣列
- `dailyPlans[*].items` 缺失或 `null` 時補空陣列
- 第一次模型輸出缺必要欄位、第二次重試成功
- 連續兩次失敗時丟出 `ModelOutputException`

### 整合測試

以 fake/stub 方式模擬：

- `INvidiaChatClient`
- `IWebSearchService`
- `IWebPageVerifier`

藉此驗證 `TravelPlannerAgent` 的：

- `RunAsync`
- session 記憶
- `reset` 清空行為

### 啟動層測試

- 無參數時進入 REPL
- `help` 只顯示用法
- `plan --request` 維持單次輸出
- 標準輸入為 EOF/null 時顯示明確提示
- REPL 中模型解析失敗時，仍可繼續接受下一輪輸入

## 文件同步規則

後續若有任何功能異動，至少同步更新：

- `README.md`
- `docs/project-plan.md`

若命令、環境變數、輸出格式、模型容錯規則或模組責任有變動，這兩份文件必須在同一次修改中一併更新。
