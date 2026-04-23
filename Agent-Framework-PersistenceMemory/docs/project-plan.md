# 專案計畫

## 目標

建立一個極簡 PM Agent 範例，完整展示下面這條流程：

`原始需求匯入 -> 工作拆解 -> 逐項討論修正 -> 正式清單輸出 -> 永久記憶保存`

## 目前已實作

- Console REPL 與 slash commands
- `/ingest` / `/end` 貼上模式
- `source + work item` 記憶模型
- 自然語言修正與 `/work update`
- `/finalize` 正式工作需求清單輸出
- UTF-8 JSON 永久記憶
- Windows UTF-8 主控台設定
- work-id 找不到時的非致命錯誤處理
- Agent 狀態訊息輸出
- README 與 docs 同步更新

## 本版特別補強的問題

### 1. 模型偏題

先前真實測試時，使用者輸入的是會員管理後台、角色權限與審計紀錄，但模型可能輸出成 Email 驗證流程。這版已加入：

- 更嚴格的 prompt
- 關鍵詞忠實度檢查
- 偏題時的保守 fallback

### 2. UTF-8 輸入穩定性

先前在 Windows 管線輸入時曾出現 `????`。這版已補上：

- `Console.InputEncoding`
- `Console.OutputEncoding`
- BOM 去除
- 輸入文字正規化

### 3. work-id 找不到導致程式退出

先前 `/work update w5 ...` 可能把錯誤一路拋到外層，出現 `啟動失敗`。現在改成：

- `UserFacingException`
- REPL 內攔截並顯示友善訊息
- 主流程不中止

## 驗證重點

- 中文輸入、JSON、正式輸出檔案都不亂碼
- `/help` 在 BOM 輸入下仍可辨識
- `/work review w5`、`/work update w5 ...` 都不會讓程式退出
- 模型偏題時會回退成待確認需求或保守正式清單
- README 與 docs 範例可對應實際 REPL 行為
