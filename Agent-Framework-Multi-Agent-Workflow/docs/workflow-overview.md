# Workflow Overview

## 流程總覽

這個範例把圖面審查切成四個 Agent，每個 Agent 只負責一個窄職責，避免 prompt 太胖，也讓輸出更容易被下一步消化。

## Agent 1: Feature Parser

輸入：

- feature JSON
- dimension data
- drawing notes

輸出：

- 零件摘要
- 基準清單
- 特徵清單
- 潛在風險

目的：

先把零散的原始資料整理成標準化摘要，讓後面的檢查 Agent 不必直接處理原始輸入。

## Agent 2: Dimension Checker

輸入：

- Feature Parser 的結構化摘要

輸出：

- 尺寸檢查摘要
- 問題清單
- 缺少尺寸清單
- 矛盾清單

目的：

專注在尺寸完整性、重複標註與矛盾描述。

## Agent 3: Tolerance Reviewer

輸入：

- Dimension Checker 的檢查結果

輸出：

- 公差檢查摘要
- 公差問題清單
- 基準缺口
- 過度標註風險

目的：

根據尺寸檢查結果，補看公差是否合理、是否有缺少基準，或有過度標註風險。

## Agent 4: Report Agent

輸入：

- Tolerance Reviewer 的檢查結果

輸出：

- Markdown 審查報告

目的：

把整個流程整理成固定格式的文件，方便人讀，也方便之後輸出到檔案、資料庫或 API。

## 資料流設計

- Workflow input: `ReviewRequest`
- Feature Parser output: `FeatureParseResult`
- Dimension Checker output: `DimensionCheckResult`
- Tolerance Reviewer output: `ToleranceReviewResult`
- Final output: `FinalReviewReport`

這種型別化資料流讓每一階段的責任清楚，也比較容易加上測試或替換 agent prompt。
