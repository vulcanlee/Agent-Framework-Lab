# AgentFunctionCall

這是一個使用 Microsoft Agent Framework、`Microsoft.Extensions.AI` 與 GitHub Models 建立的 C# CLI Agent。它接收自然語言輸入，透過本地四則運算工具完成計算，並把 LLM 與工具的互動流程完整輸出到 console。

## 核心能力
- 支援自然語言加、減、乘、除。
- 使用 GitHub Models endpoint `https://models.github.ai/inference`。
- 從系統環境變數 `GITHUB_TOKEN` 讀取 API token。
- 透過 `UseFunctionInvocation()` 讓模型能多輪呼叫 `add`、`subtract`、`multiply`、`divide`。
- 以 `>>>`、`<<<`、`???` 顯示送往 LLM、收到的內容與再次推論理由。
- 顯示 token usage，並遮蔽 Bearer token。

## 技術堆疊
- `.NET 8`
- `Microsoft.Agents.AI`
- `Microsoft.Extensions.AI`
- `OpenAI` .NET SDK
- xUnit

## 目錄結構
```text
docs/                               文件與設計說明
scripts/                            開發輔助腳本
src/AgentFunctionCall/              主程式
src/AgentFunctionCall/Prompts/      runtime prompt
src/AgentFunctionCall/Services/     Agent、tools、logger、formatter
src/tests/AgentFunctionCall.Tests/  測試
```

## 快速開始
1. 設定 `GITHUB_TOKEN`
```powershell
$env:GITHUB_TOKEN = "your-token"
```

2. 如需在 PowerShell 正確顯示中文，先啟用 UTF-8
```powershell
. .\scripts\Enable-Utf8Console.ps1
```

3. 執行應用程式
```powershell
dotnet run --project .\src\AgentFunctionCall\AgentFunctionCall.csproj
```

4. 輸入範例
```text
23 加 58
(2*3 加 10)/8
10 除以 0
```

## 常用命令
```powershell
dotnet build .\AgentFunctionCall.slnx
dotnet test .\src\tests\AgentFunctionCall.Tests\AgentFunctionCall.Tests.csproj
dotnet run --project .\src\AgentFunctionCall\AgentFunctionCall.csproj
```

## 重要行為
- 沒有 `GITHUB_TOKEN` 時，程式會直接顯示錯誤並結束。
- Agent 不直接心算；四則運算必須透過本地工具完成。
- Agent 是否繼續與 LLM 互動，取決於最新一輪回應是否仍包含新的 tool call。
- `FINAL RESPONSE` 是結果展示，不是停止條件本身。

## 文件導覽
- [專案計畫](./docs/agent-calculator-plan.md)
- [Agent 行為說明](./docs/agent.md)
- [架構說明](./docs/architecture.md)
- [Logging 與 Tracing](./docs/logging-and-tracing.md)
- [互動範例](./docs/examples.md)
- [測試指南](./docs/testing.md)
- [日常操作](./docs/operations.md)
- [GitHub Token 取得與設定](./docs/github-token-setup.md)
- [安全說明](./docs/security.md)
- [ADR: Agent loop 決策](./docs/decisions/0001-agent-loop.md)
- [AI 協作規則](./AGENTS.md)
- [協作開發指南](./CONTRIBUTING.md)
