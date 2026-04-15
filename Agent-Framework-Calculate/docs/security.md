# 安全說明

## 憑證
- GitHub Models token 透過系統環境變數 `GITHUB_TOKEN` 提供。
- Token 不應寫入 repo、測試檔、文件範例或 console log。

## Logging
- 所有輸出到 console 的內容都應經過遮蔽處理。
- `Bearer <token>` 必須顯示為 `Bearer [REDACTED]`。
- 若錯誤訊息內含敏感資訊，也要先遮蔽再輸出。

## Prompt 與資料
- runtime prompt 放在 `src/AgentFunctionCall/Prompts/agent-instructions.md`。
- prompt 可以描述工具使用規則，但不可包含真正 token 或其他秘密。

## 錯誤處理
- 啟動時若 `GITHUB_TOKEN` 缺失，只顯示明確錯誤，不輸出更多敏感環境資訊。
- LLM 呼叫失敗時，向使用者顯示可讀訊息，但不暴露憑證內容。

## 維護守則
- 改 logger、formatter 或例外處理時，要確認遮蔽規則沒有被破壞。
- 若新增新的外部服務或 secret，需同步更新本文件與對應測試。
