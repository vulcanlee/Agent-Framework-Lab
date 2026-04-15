# Implementation Guide

這份說明聚焦在「最小可運作、方便教學」的版本，不追求完整平台化，而是讓你能清楚解釋：

1. 如何接上 Microsoft Agent Framework
2. 如何把 GitHub Models 當作 OpenAI-compatible endpoint 使用
3. 如何只輸入 prompt，然後自動從 prompt 解析檔案路徑
4. 如何把 prompt 與檔案一起送進模型

## 為什麼這次改成只輸入 prompt

原本的版本需要：

1. 先輸入檔案路徑
2. 再輸入 prompt

這對操作來說沒有問題，但如果你的文章想強調「使用者只需要自然地描述需求」，那麼更好的體驗是：

- 使用者只輸入一段 prompt
- prompt 裡直接包含完整檔案路徑
- 程式自己解析出路徑並載入檔案

例如：

```text
這是一個設計圖 C:\Users\vulca\Downloads\sample.png 針對這張圖的尺寸標示是否有什麼問題
```

這種設計的好處是：

- 更接近自然語言使用情境
- 技術文章更容易示範
- 程式仍然維持簡單，不需要先引入完整 tool calling 架構

## Agent Framework 整合方式

核心流程在 `GitHubModelAnalyzer`：

1. 從 `GITHUB_TOKEN` 讀取金鑰
2. 建立 `OpenAIClient`
3. 把 endpoint 指到 `https://models.github.ai/inference`
4. 透過 `GetChatClient(model)` 取得 chat client
5. 使用 `AsAIAgent()` 轉成 Microsoft Agent Framework 的 agent
6. 建立 session，送出訊息並取得結果

這樣的好處是：

- 程式碼保留 Agent Framework 的使用方式
- 不需要自己手寫 HTTP request
- 仍然維持很小的範例尺寸

## prompt 路徑解析流程

新增的 `FilePromptResolver` 負責兩件事：

1. 如果有傳入 `--file`，直接使用 `--file`
2. 如果沒有傳入 `--file`，從 prompt 中抓取完整 Windows 路徑

這個類別使用正則表達式尋找副檔名在支援清單內的完整路徑，找到之後就交給後續檔案讀取流程。

這代表目前的實際設計是：

- 路徑解析由程式負責
- 檔案分析由 LLM 負責

這比把「找出檔案路徑」也完全交給模型更穩定，因為路徑辨識屬於可預測的字串處理，適合由本地程式直接處理。

## 文字檔與圖片檔怎麼處理

### 文字檔

文字檔會走這個流程：

1. 以 UTF-8 讀檔
2. 把原始 prompt、檔名、路徑、檔案內容組成一段完整訊息
3. 用 `TextContent` 傳給模型

這種做法很適合技術文章，因為讀者容易理解「其實就是把檔案內容包進 prompt」。

### 圖片檔

圖片檔會走多模態流程：

1. 讀取圖片位元組
2. 依副檔名判斷 MIME type
3. 建立 `DataContent(bytes, mediaType)`
4. 和 `TextContent(prompt)` 一起組成同一則使用者訊息

這樣模型就會同時拿到：

- 使用者的分析要求
- 圖片本身

因此可以直接支援設計圖檢查這種案例。

## 主要程式檔案

### `Program.cs`

負責：

- 啟動流程
- CLI 參數模式
- 互動式輸入模式
- 錯誤處理
- 顯示分析結果

### `AppOptions.cs`

負責：

- 解析 `--prompt`、`--file`、`--model`
- 判斷是否切換到互動式模式
- 驗證輸入
- 決定模型優先順序

模型優先順序如下：

1. `--model`
2. `GITHUB_MODEL`
3. `openai/gpt-4.1-mini`

### `FilePromptResolver.cs`

負責：

- 從 prompt 中找出檔案路徑
- 在 `--file` 存在時直接優先使用
- 驗證解析出的路徑是否存在

### `FileInputBuilder.cs`

負責：

- 判斷檔案是文字還是圖片
- 讀取 UTF-8 文字
- 建立多模態輸入內容
- 在不支援的副檔名時回報清楚錯誤

### `GitHubModelAnalyzer.cs`

負責：

- 建立 OpenAI-compatible client
- 建立 Agent Framework agent
- 建立 session
- 呼叫模型
- 回傳純文字結果

## 目前刻意不做的事

這份範例刻意不加入以下功能：

- 長期記憶
- 多輪對話保存
- 真正的 tool calling
- 串流顯示
- 多檔案一次分析
- PDF / OCR / 文件解析
- Web UI

原因不是做不到，而是這些功能都會讓「教學主線」變得不夠聚焦。

## 如果之後要改成真正的工具呼叫

如果你下一篇文章想延伸到「agent 自己決定要呼叫哪個本地方法」，可以往這個方向擴充：

1. 把 `FilePromptResolver` 包成可供 agent 呼叫的 function
2. 再加入 `load_file(path)` 之類的本地工具
3. 讓 agent 先從 prompt 抽出 path，再主動載入檔案

這會更貼近 Agent Framework 的 agent/tool 協作模型，但也會讓範例複雜不少，所以目前版本先保留在最小可說明的程度。
