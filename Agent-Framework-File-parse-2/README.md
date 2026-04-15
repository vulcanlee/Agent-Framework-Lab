# Microsoft Agent Framework + GitHub Models 檔案分析 Demo

這個專案示範如何用最精簡的 C# Console App，透過 Microsoft Agent Framework 連到 GitHub Models 的 OpenAI-compatible 端點，直接輸入一段 prompt，讓程式從 prompt 中自動找出檔案路徑、讀取檔案，再把 prompt 與檔案一起送給模型分析。

這份範例刻意維持簡單，方便你後續撰寫技術文章，說明如何一步一步完成這個功能。

## 專案重點

- 使用 Microsoft Agent Framework 的 `AsAIAgent()` 建立最小 agent
- 從環境變數 `GITHUB_TOKEN` 讀取 GitHub Models API Key
- 預設使用 `openai/gpt-4.1-mini`
- 支援 CLI 參數模式與互動式 Console 模式
- 只需要輸入 prompt，檔案路徑可直接寫在 prompt 內
- 支援 UTF-8 文字檔與圖片檔分析

## 專案結構

```text
.
├─ docs/
│  ├─ implementation-guide.md
│  └─ project-plan.md
├─ src/
│  └─ FileAnalysisDemo/
│     ├─ AppOptions.cs
│     ├─ FileAnalysisDemo.csproj
│     ├─ FileInput.cs
│     ├─ FileInputBuilder.cs
│     ├─ FilePromptResolver.cs
│     ├─ GitHubModelAnalyzer.cs
│     └─ Program.cs
└─ README.md
```

## 需求

- .NET 9 SDK
- GitHub Models 可用的 token
- 已設定環境變數 `GITHUB_TOKEN`

PowerShell 範例：

```powershell
$env:GITHUB_TOKEN = "YOUR_GITHUB_TOKEN"
```

如果要覆蓋預設模型，也可以設定：

```powershell
$env:GITHUB_MODEL = "openai/gpt-4.1-mini"
```

## 快速開始

先編譯：

```powershell
dotnet build .\src\FileAnalysisDemo\FileAnalysisDemo.csproj
```

### 方式 1：CLI 參數模式

現在最推薦的用法是只給 `--prompt`：

```powershell
dotnet run --project .\src\FileAnalysisDemo\FileAnalysisDemo.csproj -- `
  --prompt "這是一個設計圖 C:\Users\vulca\Downloads\sample.png，針對這張圖的尺寸標示是否有什麼問題？請列出觀察重點。"
```

如果你想手動指定檔案，也還是可以用 `--file`：

```powershell
dotnet run --project .\src\FileAnalysisDemo\FileAnalysisDemo.csproj -- `
  --file "C:\Users\vulca\Downloads\sample.png" `
  --prompt "針對這張圖的尺寸標示是否有什麼問題？"
```

### 方式 2：互動式模式

不帶 `--prompt` 時，程式會改成逐步詢問：

```powershell
dotnet run --project .\src\FileAnalysisDemo\FileAnalysisDemo.csproj
```

畫面只需要輸入：

```text
Prompt:
```

例如：

```text
這是一個設計圖 C:\Users\vulca\Downloads\sample.png 針對這張圖的尺寸標示是否有甚麼問題
```

## 檔案路徑解析規則

- 如果有提供 `--file`，優先使用 `--file`
- 如果沒有提供 `--file`，程式會從 prompt 內抓取完整 Windows 路徑
- prompt 內的路徑必須是完整路徑，例如 `C:\Users\vulca\Downloads\sample.png`

## 支援的檔案類型

### 文字檔

這些檔案會以 UTF-8 方式讀入，內容會直接附加到 prompt 內：

- `.txt`
- `.md`
- `.json`
- `.csv`
- `.xml`
- `.cs`
- `.js`
- `.ts`
- `.tsx`
- `.jsx`
- `.html`
- `.css`
- `.yml`
- `.yaml`

### 圖片檔

這些檔案會以多模態內容送給模型：

- `.png`
- `.jpg`
- `.jpeg`
- `.webp`

## Prompt 範例

### 圖片分析

```text
這是一個設計圖 C:\Users\vulca\Downloads\sample.png 針對這張圖的尺寸標示是否有甚麼問題
```

### 文字檔分析

```text
請分析這個檔案 C:\Vulcan\Projects\Agent記憶與持續性\src\FileAnalysisDemo\AppOptions.cs，說明它的用途與主要行為
```

## 實作說明

- [docs/implementation-guide.md](C:\Vulcan\Projects\Agent記憶與持續性\docs\implementation-guide.md)
- [docs/project-plan.md](C:\Vulcan\Projects\Agent記憶與持續性\docs\project-plan.md)

## 常見錯誤

### `GITHUB_TOKEN is not set`

代表沒有設定 `GITHUB_TOKEN`。

```powershell
$env:GITHUB_TOKEN = "YOUR_GITHUB_TOKEN"
```

### `No file path was provided`

代表沒有傳入 `--file`，而且 prompt 裡也沒有完整檔案路徑。

### `The file path found in the prompt does not exist`

代表程式有從 prompt 抓到路徑，但該檔案不存在。

### `Unsupported file type`

代表目前輸入的副檔名不在 demo 支援範圍內。這份範例刻意只保留常見文字檔與圖片檔，避免教學變得太複雜。

### `not valid UTF-8 text`

代表該文字檔不是 UTF-8 編碼。這份 demo 為了保持文章重點，僅示範 UTF-8 文字檔讀取流程。

## 後續可延伸方向

- 把 prompt 內的多個檔案路徑都解析出來
- 真正加入 tool calling，讓 agent 明確呼叫本地檔案工具
- 支援 PDF、DOCX 或多檔案分析
- 加入串流輸出
- 建立 Web API 或 Web UI
- 保存 session 記憶或分析歷史
- 增加結構化 JSON 輸出
