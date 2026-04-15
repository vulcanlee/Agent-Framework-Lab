# CONTRIBUTING.md

這份文件是給人類協作者的簡短開發指南，目標是讓每次修改都能維持一致的程式行為、文件品質與驗證流程。

## 開發前
- 先閱讀 [README.md](./README.md)。
- 若要理解 Agent 運作，先看 [docs/agent.md](./docs/agent.md) 與 [docs/architecture.md](./docs/architecture.md)。
- 若要修改 prompt、logging 或停止條件，請先看 [AGENTS.md](./AGENTS.md) 了解同步更新要求。

## 本機環境
- 需要 `.NET 8`
- 需要 `GITHUB_TOKEN`
- 若在 PowerShell 顯示中文有問題，可先執行：
```powershell
. .\scripts\Enable-Utf8Console.ps1
```

## 常用命令
```powershell
dotnet build .\AgentFunctionCall.slnx
dotnet test .\src\tests\AgentFunctionCall.Tests\AgentFunctionCall.Tests.csproj
dotnet run --project .\src\AgentFunctionCall\AgentFunctionCall.csproj
```

## 修改守則
- 改 prompt 時，同步更新文件與測試。
- 改 logging 格式時，同步更新 examples 與 formatter 測試。
- 改公開行為時，同步更新 README 與對應 docs。
- 不要把 Bearer token、`GITHUB_TOKEN` 或其他敏感內容寫入 log。

## 送出前檢查
- build 成功
- test 通過
- 文件連結正確
- 中文顯示正常
- 若新增功能，已補至少一組範例與對應說明
