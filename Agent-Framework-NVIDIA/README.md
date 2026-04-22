# NVIDIA Travel Agent

這是一個用 `C# Console` 實作的旅遊規劃 Agent 範例，介面採用 `CLI` / `REPL`，核心流程會先分析需求、判斷規劃策略、搜尋網路最新資訊、驗證來源，再整理成一份附來源的旅遊行程建議清單。

目前程式預設使用 [GitHub Models](https://models.github.ai/inference/) 相容端點，仍保留 NVIDIA API key 命名的相容讀取方式，方便切換模型供應商時重用既有環境變數設定。

## 功能特色

- 支援 `plan` 與 `repl` 兩種使用方式
- 執行中會在螢幕顯示簡潔進度，例如分析需求、搜尋資料、驗證來源、整理清單
- 模型輸出會先做 schema normalization，再進行一次自動重試
- 最終輸出固定為 Markdown 格式的旅遊行程建議清單
- 行程中的景點、交通、住宿與餐飲資訊必須能對應到已驗證來源

## 環境變數

程式會依序讀取以下 API key：

1. `GITHUB_TOKEN`
2. `Navidia_Vulcan`
3. `NVIDIA_API_KEY`

PowerShell 範例：

```powershell
$env:GITHUB_TOKEN="your-github-models-token"
```

如果你要改回 NVIDIA 端點，可以調整 [AppOptions.cs](/C:/Vulcan/Projects/Agent-Framework-Lab/Agent-Framework-NVIDIA/NvidiaTravelAgent/Configuration/AppOptions.cs) 的 `Model` 與 `BaseUri` 預設值。

## 執行方式

還原與建置：

```powershell
dotnet restore .\NvidiaTravelAgent.slnx
dotnet build .\NvidiaTravelAgent.slnx
```

預設進入 REPL：

```powershell
dotnet run --project .\NvidiaTravelAgent\NvidiaTravelAgent.csproj
```

明確指定 REPL：

```powershell
dotnet run --project .\NvidiaTravelAgent\NvidiaTravelAgent.csproj -- repl
```

單次產生行程：

```powershell
dotnet run --project .\NvidiaTravelAgent\NvidiaTravelAgent.csproj -- plan --request "三天兩夜香港在地美食行程，想找平民餐飲與蛋塔推薦"
```

REPL 指令：

- `reset`：清空目前 session
- `exit`：離開 REPL

## 執行中畫面

執行 `plan` 或 `repl` 時，畫面會顯示這類進度訊息：

```text
[進度] 正在分析旅遊需求...
[進度] 這次需求較複雜，將先分步整理候選建議...
[進度] 正在搜尋：香港 在地美食 最新資訊
[進度] 正在驗證 5 筆候選來源...
[進度] 正在整理旅遊行程建議清單...
```

進度訊息只描述目前階段，不會和最終行程內容混在一起。

## 專案結構

```text
NvidiaTravelAgent.slnx
NvidiaTravelAgent/
NvidiaTravelAgent.Tests/
docs/
```

主要檔案：

- [Program.cs](/C:/Vulcan/Projects/Agent-Framework-Lab/Agent-Framework-NVIDIA/NvidiaTravelAgent/Program.cs)：CLI / REPL 入口與進度顯示
- [TravelPlannerAgent.cs](/C:/Vulcan/Projects/Agent-Framework-Lab/Agent-Framework-NVIDIA/NvidiaTravelAgent/Agents/TravelPlannerAgent.cs)：Microsoft Agent Framework agent
- [TravelPlannerEngine.cs](/C:/Vulcan/Projects/Agent-Framework-Lab/Agent-Framework-NVIDIA/NvidiaTravelAgent/Services/TravelPlannerEngine.cs)：主流程編排與 progress 回報
- [NvidiaChatClient.cs](/C:/Vulcan/Projects/Agent-Framework-Lab/Agent-Framework-NVIDIA/NvidiaTravelAgent/Services/NvidiaChatClient.cs)：模型呼叫、JSON normalization 與重試
- [WebSearchService.cs](/C:/Vulcan/Projects/Agent-Framework-Lab/Agent-Framework-NVIDIA/NvidiaTravelAgent/Services/WebSearchService.cs)：搜尋候選來源
- [WebPageVerifier.cs](/C:/Vulcan/Projects/Agent-Framework-Lab/Agent-Framework-NVIDIA/NvidiaTravelAgent/Services/WebPageVerifier.cs)：抓取並驗證網頁資訊
- [ItineraryComposer.cs](/C:/Vulcan/Projects/Agent-Framework-Lab/Agent-Framework-NVIDIA/NvidiaTravelAgent/Services/ItineraryComposer.cs)：輸出最終 Markdown 清單

## 測試

```powershell
dotnet test .\NvidiaTravelAgent.Tests\NvidiaTravelAgent.Tests.csproj
```

目前測試涵蓋：

- API key 讀取優先順序
- `TravelRequest` JSON 解析
- 搜尋結果正規化
- HTML 驗證與事實萃取
- JSON normalization / retry
- CLI / REPL 進度輸出
- 失敗情境下的友善錯誤與 progress 顯示
