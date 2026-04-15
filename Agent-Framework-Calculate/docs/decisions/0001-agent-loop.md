# ADR 0001：採用 Agent Tool Invocation Loop

## 狀態
Accepted

## 背景
這個專案的目標不是只做單純四則運算，而是示範一個可觀測的 Agent：使用者輸入自然語言後，模型能規劃步驟、呼叫本地工具、根據結果再次推論，直到得到答案。

## 決策
- 使用 Microsoft Agent Framework 與 `Microsoft.Extensions.AI`
- 使用 GitHub Models 作為推論 endpoint
- 使用本地四則運算工具作為唯一計算來源
- 使用 `UseFunctionInvocation()` 啟用多輪工具呼叫迴圈
- 使用 `>>>`、`<<<`、`???` 與 token usage 形成完整可觀測性

## 理由
- 這種方式能清楚區分模型規劃與本地工具執行。
- 可直接觀察多輪推論，而不是讓模型黑盒子地直接回答。
- 對教學、除錯、文件化與後續擴充都更友善。

## 影響
- Agent 可以處理超過單一步驟的算式，只要模型能把問題拆成多次工具呼叫。
- 停止條件取決於回應是否仍包含新的 tool call，而不是某句固定文字。
- logging、docs、tests 必須一起維持，否則可觀測性容易失真。
