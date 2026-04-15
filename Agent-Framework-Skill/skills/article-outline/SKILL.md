---
name: article-outline
description: Generate a concise technical article outline for demos, tutorials, or architecture walkthroughs. Use when the user asks for an article structure, tutorial flow, blog post plan, 技術文章大綱, 教學文章架構, or 教程章節規劃.
license: MIT
---

# Article Outline Skill

Use this skill when the user wants a technical article outline, tutorial flow, or an explainable demo structure.

## Goal

Help the answer feel easy to turn into a blog post or internal document.

## Steps

1. Infer a practical article title from the user's request.
2. Produce a compact outline with 5 to 7 sections.
3. Favor a teaching sequence:
   - problem to solve
   - solution overview
   - project structure
   - key implementation points
   - demo or validation
   - next steps
4. Keep each section to one short sentence unless the user asks for more detail.
5. If the user is writing for engineers, prefer concrete headings over marketing language.

## Output style

- Return Traditional Chinese unless the user asks for another language.
- Make the outline immediately reusable in markdown.
- If helpful, add a one-line "適合展示的重點" section at the end.
