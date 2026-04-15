# Project Plan

## 目標

建立一個最小可教學的 C# Console 範例，使用 Microsoft Agent Framework 搭配 GitHub Models，支援把單一檔案與 prompt 一起送給模型分析，並在主控台顯示結果。

## 範圍

- 支援 CLI 參數模式
- 支援互動式模式
- 支援 UTF-8 文字檔
- 支援常見圖片檔
- 產出適合技術文章的 README 與實作說明

## 設計決策

- 採用 .NET 9 Console App
- 採用 Microsoft Agent Framework 的 `AsAIAgent()` 做最小 agent 包裝
- GitHub Models endpoint 固定為 `https://models.github.ai/inference`
- `GITHUB_TOKEN` 為必要環境變數
- `GITHUB_MODEL` 可選，預設 `openai/gpt-4.1-mini`
- 不加入 session persistence、tool calling、Web API 或串流

## 驗證項目

- CLI 模式可成功分析文字檔
- 互動式模式可成功分析文字檔
- 圖片模式可分析 `sample.png`
- 缺少 `GITHUB_TOKEN` 時會有明確錯誤
- 檔案不存在時會有明確錯誤
- 不支援副檔名時會有明確錯誤

## 教學定位

這份專案不是完整產品，而是技術文章用的最小工作範例。重點是讓讀者快速理解整合方式，並保留後續延伸空間。
