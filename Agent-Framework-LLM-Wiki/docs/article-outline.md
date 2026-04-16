# 技術文章大綱

## 1. 為什麼要做 LLM Wiki

- 即時 RAG 的限制
- 持久化 wiki 的價值

## 2. 專案需求

- 可以 ingest 文件與會議紀錄
- 可以用 wiki 回答問題
- 程式碼要夠簡單，方便教學

## 3. 用 Microsoft Agent Framework 做單代理

- `ChatClientAgent`
- 自訂 `IChatClient`

## 4. 接 GitHub Models inference API

- `GITHUB_TOKEN`
- request / response

## 5. Ingest / Ask / Lint 三條主流程

- ingest 如何把資料編譯成 wiki
- ask 如何只根據 wiki 回答
- lint 如何做 wiki 健康檢查

## 6. 測試與延伸

- 單元測試
- fake agent 整合測試
- 後續可加的功能
