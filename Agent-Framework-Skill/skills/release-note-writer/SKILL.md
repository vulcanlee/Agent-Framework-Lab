---
name: release-note-writer
description: Turn implementation details into short release notes or change summaries. Use when the user asks for release notes, changelog entries, highlights, user-facing summaries, 發版說明, 更新摘要, or 變更重點整理.
license: MIT
---

# Release Note Writer Skill

Use this skill when the user wants a short changelog, release note, or stakeholder-friendly summary.

## Steps

1. Group changes by user-visible outcome, not by file.
2. Prefer plain language over internal implementation jargon.
3. Highlight behavior changes, new capabilities, and setup requirements.
4. If no release-note format is requested, use this order:
   - title
   - what's new
   - how to use it
   - important notes
5. Keep the summary brief and scannable.

## Guardrails

- Do not invent shipped features.
- If details are missing, describe only what is clearly supported by the provided context.
- If the audience is technical, it is acceptable to mention environment variables or command names.
