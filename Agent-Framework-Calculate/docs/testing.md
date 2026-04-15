# 測試指南

這個專案目前以本地單元測試為主，搭配 build 驗證與手動互動測試。

## 目前已有的測試
- `CalculatorToolsTests`
  - 驗證加減乘除結果
  - 驗證除以 0 的錯誤
  - 驗證工具 log 與 `???` 推進說明
- `InteractionLoggingFormatterTests`
  - 驗證 `>>>`、`<<<`、`???` 前綴
  - 驗證 token usage 格式
  - 驗證敏感資訊遮蔽
- `AppConfigurationTests`
  - 驗證 `GITHUB_TOKEN` 缺失時會失敗
  - 驗證 endpoint、model、token 載入
- `AgentInstructionsTests`
  - 驗證 prompt 載入與 fallback 行為

## 必跑命令
```powershell
dotnet build .\AgentFunctionCall.slnx
dotnet test .\src\tests\AgentFunctionCall.Tests\AgentFunctionCall.Tests.csproj
```

## 手動驗證
設好 `GITHUB_TOKEN` 後：
```powershell
dotnet run --project .\src\AgentFunctionCall\AgentFunctionCall.csproj
```

建議至少測這幾組：
- `23 加 58`
- `90 減 17`
- `12 乘以 12`
- `15 除以 4`
- `(2*3 加 10)/8`
- `10 除以 0`
- `今天天氣如何`

## 哪些不需要 GITHUB_TOKEN
- `dotnet build`
- `dotnet test`
- prompt 載入與 formatter 類測試

## 哪些需要 GITHUB_TOKEN
- 真正呼叫 GitHub Models 的互動式執行
- 任何要觀察遠端 LLM 真實 response 與 token usage 的測試

## 維護規則
- 改 prompt、logging、停止條件或工具名稱時，必須同步更新測試。
- 改 examples 或文件中的命令時，要確認這些命令仍可實際執行。
