# LLM Wiki 方法在本專案中的映射

Karpathy 提出的重點是先讓 LLM 維護一個持久化 wiki，再從 wiki 回答問題。

本專案把這個方法拆成三層：

## Raw sources

`raw/` 是不可變來源層。

## Wiki

`wiki/` 是可讀、可引用、可檢查的 markdown 知識層。

- `sources/`
- `topics/`
- `entities/`
- `analyses/`

## Query

查詢時先搜 `wiki/index.md` 與既有頁面，再由 Agent 綜合回答。

這和即時 RAG 最大的差異是：

- 新知識會寫回 wiki
- 關聯與摘要會累積
- 問答依賴的是已整理過的知識層

## Lint

wiki 不只需要新增內容，也需要維護健康度，所以專案把 `lint` 視為一級操作。
