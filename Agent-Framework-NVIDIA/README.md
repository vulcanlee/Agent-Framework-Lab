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
- 模型輸出會經過 schema normalization，並在必要時自動重試一次
- 最終輸出固定包含來源清單

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
dotnet run --project .\NvidiaTravelAgent\NvidiaTravelAgent.csproj -- plan --request "三天兩夜台南美食散步，不自駕，預算中等"
```

REPL 內建指令：

- `reset`：清空目前 session
- `exit`：離開 REPL

## 啟動規則

- 無參數：預設進入 REPL
- `help` / `--help` / `-h`：顯示說明
- `plan --request "..."`：輸出單次行程規劃
- `repl`：明確指定 REPL 模式

如果模型回傳的 JSON 形狀不完全符合預期，程式會先做 normalization，必要時再自動重試一次；若仍失敗，REPL 會顯示友善錯誤訊息，而不是直接噴出 `JsonException` 堆疊。

## 專案結構

```text
NvidiaTravelAgent.slnx
NvidiaTravelAgent/
NvidiaTravelAgent.Tests/
docs/
```

主要模組：

- `Configuration/AppOptions.cs`：讀取 API Key 與模型設定
- `Services/NvidiaChatClient.cs`：封裝 NVIDIA API 呼叫與 JSON 容錯
- `Services/ModelJsonNormalizer.cs`：針對模型輸出做 schema normalization
- `Services/WebSearchService.cs`：搜尋候選來源
- `Services/WebPageVerifier.cs`：抓取並驗證網頁內容
- `Services/TravelPlannerEngine.cs`：協調需求解析、搜尋驗證、行程生成
- `Agents/TravelPlannerAgent.cs`：Microsoft Agent Framework 入口

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
- `specialRequirements` / `transportationNotes` 的 normalization
- 第一次錯誤、第二次重試成功的 JSON 修復流程

## 設計取向

這個範例刻意保持簡單，方便後續撰寫技術文章：

- 不加入資料庫
- 不加入 Web UI
- 不做訂位與付款
- 不保證即時價格
- 專注在「可查證、可說明、可示範」的 Agent 流程

更完整的技術說明請見 [docs/project-plan.md](/C:/Vulcan/Projects/Agent-Framework-Lab/Agent-Framework-NVIDIA/docs/project-plan.md)。
