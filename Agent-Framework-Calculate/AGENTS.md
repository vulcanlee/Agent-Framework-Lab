# AGENTS.md

這份文件是給 Codex / AI 代理使用的專案協作規則。目標是讓任何代理在進入這個 repo 時，都能用一致方式理解 source of truth、進行修改、更新文件並完成驗證。

## 專案目的
- 這個 repo 提供一個 C# CLI Agent。
- Agent 使用 Microsoft Agent Framework 與 GitHub Models。
- Agent 只支援四則運算，但可把複合算式拆成多輪工具呼叫。
- 重點不是複雜數學，而是清楚展示 LLM、tool call、token usage 與停止條件。

## Source Of Truth
- 功能需求與規格：`docs/agent-calculator-plan.md`
- Agent 行為與停止條件：`docs/agent.md`
- runtime prompt：`src/AgentFunctionCall/Prompts/agent-instructions.md`
- logging 格式與可觀測性規格：`docs/logging-and-tracing.md`
- 實際程式行為：`src/AgentFunctionCall/Services/`

## 修改規則
- 改 prompt 規則時，必須同步檢查：
  - `docs/agent.md`
  - `docs/agent-calculator-plan.md`
  - `src/AgentFunctionCall/Prompts/agent-instructions.md`
  - 相關測試
- 改 logging 格式時，必須同步檢查：
  - `docs/logging-and-tracing.md`
  - `docs/examples.md`
  - `src/tests/AgentFunctionCall.Tests/InteractionLoggingFormatterTests.cs`
  - `src/tests/AgentFunctionCall.Tests/CalculatorToolsTests.cs`
- 改 Agent loop、工具選擇、停止條件時，必須同步檢查：
  - `docs/agent.md`
  - `docs/architecture.md`
  - `docs/decisions/0001-agent-loop.md`

## 不可破壞的行為
- 不可移除 `>>>`、`<<<`、`???` 的可觀測性格式，除非同步更新所有相關文件與測試。
- 不可在 log 中輸出 `GITHUB_TOKEN` 或未遮蔽的 Bearer token。
- 不可把計算邏輯偷偷改成由模型直接心算，工具必須保留為實際計算來源。
- 不可宣稱完成而不做至少對應的 build/test 驗證。

## 驗證要求
- 修改文件後，至少確認連結與命令沒有過時。
- 修改程式後，至少執行：
```powershell
dotnet build .\AgentFunctionCall.slnx
dotnet test .\src\tests\AgentFunctionCall.Tests\AgentFunctionCall.Tests.csproj
```
- 若改到中文文件或 console 顯示，應確認 UTF-8 可讀性；必要時先執行：
```powershell
. .\scripts\Enable-Utf8Console.ps1
```

## 文件維護規則
- 新增功能時，要補：
  - 使用方式
  - 行為規格
  - 至少一組 examples
  - 對應測試說明或測試更新
- 不要把同一段規格複製到多份文件。優先維護 source-of-truth，再在其他文件連結引用。
- 所有新增文件使用 UTF-8，主要語言為繁體中文。
