# 日常操作

這份文件整理日常開發、檢查與排錯時最常用的操作。

## 啟用 PowerShell UTF-8
```powershell
. .\scripts\Enable-Utf8Console.ps1
```

## 建置
```powershell
dotnet build .\AgentFunctionCall.slnx
```

## 測試
```powershell
dotnet test .\src\tests\AgentFunctionCall.Tests\AgentFunctionCall.Tests.csproj
```

## 執行應用程式
```powershell
$env:GITHUB_TOKEN = "your-token"
dotnet run --project .\src\AgentFunctionCall\AgentFunctionCall.csproj
```

若還沒有 token，先參考 [GitHub Token 取得與設定](./github-token-setup.md)。

## 如何觀察 logs
- `>>>` 看送給 LLM 的內容
- `<<<` 看 LLM 或工具回來的內容
- `???` 看為什麼系統準備再次推論
- `<<< TOKEN USAGE` 看 token 耗用

## 常見失敗
### 沒有設定 `GITHUB_TOKEN`
現象：
```text
必須先設定系統環境變數 GITHUB_TOKEN。
```
處理：
- 先在目前 shell 設定 `$env:GITHUB_TOKEN`

### 中文顯示亂碼
處理：
- 先執行 `. .\scripts\Enable-Utf8Console.ps1`
- 再用 `Get-Content <path> -Encoding utf8`

### 沒有看到 token usage
可能原因：
- 呼叫在失敗前沒有收到完整回應
- SDK 沒有回傳 usage
預期行為：
- 系統會顯示 `無法取得 token usage。`
