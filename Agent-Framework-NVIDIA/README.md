# NVIDIA Travel Agent

這是一個以 `C# Console` 撰寫的 Microsoft Agent Framework 範例專案，示範如何串接 NVIDIA 免費模型資源 `meta/llama-4-maverick-17b-128e-instruct`，打造一個可用 `CLI` 或 `REPL` 操作的旅遊行程規劃 Agent。

Agent 的核心原則很單純：

- 使用者先用自然語言描述需求
- 程式先把需求結構化
- 再搜尋網路上的最新公開資訊
- 驗證景點、交通、住宿等資訊是否真的存在
- 最後只根據已驗證資訊生成行程

未驗證的資訊不會被放進最終輸出。

## 功能特色

- 採用 Microsoft Agent Framework 的自訂 `AIAgent`
- 使用 NVIDIA Chat Completions API，不依賴額外搜尋 API Key
- 支援 `plan` 單次輸出模式
- 支援 `repl` 連續對話模式
- 無參數時預設直接進入 REPL
- 若執行環境沒有互動式標準輸入，REPL 會顯示提示後結束
- 最終輸出固定包含來源清單
- `README.md` 與 `docs/project-plan.md` 皆以 UTF-8 編碼維護

## 環境需求

- .NET SDK 9.0 以上
- 可連外網路
- NVIDIA API Key

請先設定環境變數，優先使用 `Navidia_Vulcan`：

```powershell
$env:Navidia_Vulcan="your-nvidia-api-key"
```

若未設定，程式會嘗試使用：

```powershell
$env:NVIDIA_API_KEY="your-nvidia-api-key"
```

## 快速開始

安裝與建置：

```powershell
dotnet restore .\NvidiaTravelAgent.slnx
dotnet build .\NvidiaTravelAgent.slnx
```

直接啟動 REPL：

```powershell
dotnet run --project .\NvidiaTravelAgent\NvidiaTravelAgent.csproj
```

明確指定 REPL：

```powershell
dotnet run --project .\NvidiaTravelAgent\NvidiaTravelAgent.csproj -- repl
```

執行單次旅遊規劃：

```powershell
dotnet run --project .\NvidiaTravelAgent\NvidiaTravelAgent.csproj -- plan --request "三天兩夜去香港旅遊，想要體驗與品嘗當地居民最喜歡去吃的各種平民餐飲，體驗各種煙火氣息，建議各種香港必須吃的餐點(我已經去過三次香港了)，另外，我還需要購買多家好吃的蛋塔，為什麼建議這家，我會外帶回飯店吃，請幫我規劃行程，並且提供每個行程的資訊來源"
```

REPL 內建指令：

- `reset`：清空目前 session
- `exit`：離開 REPL

## 啟動規則

- 無參數：預設進入 REPL
- `help` / `--help` / `-h`：顯示說明
- `plan --request "..."`：輸出單次行程規劃
- `repl`：明確指定 REPL 模式

如果執行環境沒有可互動的標準輸入，REPL 會顯示「沒有可互動的標準輸入」提示，而不是看起來像正常執行後秒退。

## 專案結構

```text
NvidiaTravelAgent.slnx
NvidiaTravelAgent/
NvidiaTravelAgent.Tests/
docs/
```

主要模組：

- `Configuration/AppOptions.cs`：讀取 API Key 與模型設定
- `Services/NvidiaChatClient.cs`：封裝 NVIDIA API 呼叫
- `Services/WebSearchService.cs`：搜尋候選來源
- `Services/WebPageVerifier.cs`：抓取並驗證網頁內容
- `Services/TravelPlannerEngine.cs`：協調需求解析、搜尋驗證、行程生成
- `Agents/TravelPlannerAgent.cs`：Microsoft Agent Framework 入口

## 執行流程

1. 使用者輸入自由文字需求
2. `TravelPlannerEngine` 呼叫 NVIDIA 模型解析成 `TravelRequest`
3. 程式產生景點、交通、住宿三類搜尋查詢
4. `WebSearchService` 搜尋候選來源
5. `WebPageVerifier` 擷取可引用的標題、摘要與關鍵事實
6. 程式將已驗證資料交給 NVIDIA 模型生成 `TravelPlan`
7. `ItineraryComposer` 檢查每個行程項目是否都有來源，再輸出最終文字

## 測試

```powershell
dotnet test .\NvidiaTravelAgent.Tests\NvidiaTravelAgent.Tests.csproj
```

目前測試涵蓋：

- `Navidia_Vulcan` 與 fallback 環境變數讀取
- `TravelRequest` JSON 反序列化
- 搜尋結果去重與無效網址排除
- HTML 驗證器的標題、摘要與事實萃取
- 行程輸出時的來源約束
- `TravelPlannerAgent` 的 session 記憶與 reset 行為
- 無參數啟動時會進入 REPL
- EOF/null 輸入時會顯示明確提示

## 設計取向

這個範例刻意保持簡單，方便後續撰寫技術文章：

- 不加入資料庫
- 不加入 Web UI
- 不做訂位與付款
- 不保證即時價格
- 專注在「可查證、可說明、可示範」的 Agent 流程

更完整的技術說明請見 [docs/project-plan.md](/C:/Vulcan/Projects/Agent-Framework-Lab/Agent-Framework-NVIDIA/docs/project-plan.md)。
