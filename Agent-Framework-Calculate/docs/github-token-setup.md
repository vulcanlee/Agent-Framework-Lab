# GitHub Token 取得與設定

這份文件說明如何透過 GitHub 網站建立 Personal Access Token，並在 Windows 上把它設定到系統環境變數 `GITHUB_TOKEN`。

## 先選 token 類型
GitHub 官方目前同時支援兩種 Personal Access Token：

- `Fine-grained personal access token`
  - GitHub 官方較推薦。
  - 可以限制可存取的 repository 與權限範圍。
- `Personal access token (classic)`
  - 權限較廣。
  - 若只需要做簡單測試通常也能使用，但風險較高。

對這個專案，建議優先使用 `Fine-grained personal access token`。

## 透過 GitHub 網站建立 token
根據 GitHub 官方文件，目前建立 token 的入口是：

1. 登入 GitHub。
2. 右上角點擊個人頭像。
3. 點擊 `Settings`。
4. 左側選單點擊 `Developer settings`。
5. 在 `Personal access tokens` 下，選擇：
   - `Fine-grained tokens`，或
   - `Tokens (classic)`

### 建立 Fine-grained token
1. 進入 `Fine-grained tokens`。
2. 點擊 `Generate new token`。
3. 填入：
   - `Token name`
   - `Expiration`
   - `Description`（可選）
4. 設定可存取的 owner / repositories。
5. 設定所需 permissions。
6. 點擊 `Generate token`。
7. 立刻把 token 複製並保存，因為之後通常不會再完整顯示。

### 建立 Classic token
1. 進入 `Tokens (classic)`。
2. 選擇 `Generate new token`。
3. 再點一次 `Generate new token (classic)`。
4. 填入：
   - `Note`
   - `Expiration`
   - 需要的 scopes
5. 點擊 `Generate token`。
6. 立刻把 token 複製並保存。

## 這個專案建議的最小權限
如果只是要呼叫 GitHub Models 做推論，原則上應採最小權限，不要額外勾選與 repository 管理無關的高權限設定。

實務上：
- 若你的 GitHub 帳號或組織對 token 有額外限制，需依組織政策調整。
- 若 token 需用於組織資源，可能還要額外完成組織授權或 SSO 授權。

## 在 Windows 設定 `GITHUB_TOKEN`

### 只對目前 PowerShell 視窗生效
這種方式最簡單，但只在目前 shell session 有效，關閉視窗就失效。

```powershell
$env:GITHUB_TOKEN = "你的 GitHub Token"
```

可用下面命令確認：

```powershell
$env:GITHUB_TOKEN
```

### 永久設定為使用者環境變數
這種方式會寫到目前使用者的環境變數，之後重開 PowerShell 或命令列仍可使用。

PowerShell:

```powershell
[System.Environment]::SetEnvironmentVariable("GITHUB_TOKEN", "你的 GitHub Token", "User")
```

Cmd / setx:

```powershell
setx GITHUB_TOKEN "你的 GitHub Token"
```

設定完成後，請重新開啟一個新的 PowerShell 視窗，再執行：

```powershell
$env:GITHUB_TOKEN
```

### 永久設定為系統環境變數
這種方式會寫到整台 Windows 的系統層級環境變數，通常需要系統管理員權限。

PowerShell:

```powershell
[System.Environment]::SetEnvironmentVariable("GITHUB_TOKEN", "你的 GitHub Token", "Machine")
```

Cmd / setx:

```powershell
setx GITHUB_TOKEN "你的 GitHub Token" /m
```

設定完成後，同樣需要重新開新的 shell 視窗再讀取。

## 如何驗證專案已讀到 token
設定完成後，可直接執行：

```powershell
dotnet run --project .\src\AgentFunctionCall\AgentFunctionCall.csproj
```

如果沒有看到：

```text
必須先設定系統環境變數 GITHUB_TOKEN。
```

就代表程式已成功讀到 `GITHUB_TOKEN`。

## 安全注意事項
- 不要把 token 寫進程式碼、文件範例或測試檔。
- 不要把 token commit 到 git。
- 不要把 token 貼進 issue、聊天室或 log。
- 若懷疑 token 外洩，應立即到 GitHub token 管理頁面 revoke 並重建。

## 官方參考文件
- GitHub Docs: [Managing your personal access tokens](https://docs.github.com/github/authenticating-to-github/creating-a-personal-access-token-for-the-command-line)
- GitHub Docs: [Managing your personal access tokens (Enterprise Cloud)](https://docs.github.com/enterprise-cloud%40latest//authentication/keeping-your-account-and-data-secure/creating-a-personal-access-token)
- Microsoft Learn: [setx](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/setx)
- Microsoft Learn: [Use environment variables](https://learn.microsoft.com/en-us/azure/ai-services/cognitive-services-environment-variables)
