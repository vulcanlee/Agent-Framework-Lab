# 互動範例

以下範例以目前專案的實際 log 風格為準，目的是讓人快速理解 Agent 如何工作。

## 單步加法
輸入：
```text
23 加 58
```

可能看到：
```text
>>> USER INPUT: 23 加 58
>>> SYSTEM PROMPT: ...
>>> USER MESSAGE: ...
<<< TOOL CALL REQUEST: add
<<< ARGUMENTS: a=23, b=58
<<< TOOL RESULT: 81
??? 已完成工具呼叫：add(a=23, b=58)
??? 目前已知結果：81
??? 為何需要再次推論：系統需要把最新工具結果帶回 LLM，確認整體問題是否已完成，或是否還需要下一個工具呼叫。
??? 下一輪目標：請 LLM 依據目前上下文決定下一步，可能是繼續呼叫工具或直接產生最終答案。
<<< FINAL RESPONSE: 23 加 58 的結果是 81。
<<< TOKEN USAGE: ...
```

## 多步複合算式
輸入：
```text
(2*3 加 10)/8
```

可能看到：
```text
>>> USER INPUT: (2*3 加 10)/8
>>> SYSTEM PROMPT: ...
>>> USER MESSAGE: ...
<<< TOOL CALL REQUEST: multiply
<<< ARGUMENTS: a=2, b=3
<<< TOOL RESULT: 6
??? 已完成工具呼叫：multiply(a=2, b=3)
??? 目前已知結果：6
??? 為何需要再次推論：系統需要把最新工具結果帶回 LLM，確認整體問題是否已完成，或是否還需要下一個工具呼叫。
??? 下一輪目標：請 LLM 依據目前上下文決定下一步，可能是繼續呼叫工具或直接產生最終答案。
>>> FOLLOW-UP PAYLOAD: ...
<<< TOOL CALL REQUEST: add
<<< ARGUMENTS: a=6, b=10
<<< TOOL RESULT: 16
??? 已完成工具呼叫：add(a=6, b=10)
??? 目前已知結果：16
??? 為何需要再次推論：系統需要把最新工具結果帶回 LLM，確認整體問題是否已完成，或是否還需要下一個工具呼叫。
??? 下一輪目標：請 LLM 依據目前上下文決定下一步，可能是繼續呼叫工具或直接產生最終答案。
>>> FOLLOW-UP PAYLOAD: ...
<<< TOOL CALL REQUEST: divide
<<< ARGUMENTS: a=16, b=8
<<< TOOL RESULT: 2
<<< FINAL RESPONSE: 答案是 2。
<<< TOKEN USAGE: ...
```

## 除以 0
輸入：
```text
10 除以 0
```

可能看到：
```text
>>> USER INPUT: 10 除以 0
<<< TOOL CALL REQUEST: divide
<<< ARGUMENTS: a=10, b=0
<<< TOOL ERROR: 不可除以 0。
<<< ERROR: 不可除以 0。
```

## 非支援請求
輸入：
```text
今天天氣如何
```

可能看到：
```text
>>> USER INPUT: 今天天氣如何
>>> SYSTEM PROMPT: ...
>>> USER MESSAGE: ...
<<< FINAL RESPONSE: 目前只支援加減乘除相關的請求。
<<< TOKEN USAGE: ...
```
