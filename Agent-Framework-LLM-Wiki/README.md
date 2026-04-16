# Agent-Test

This project is a C# CLI prototype for building an LLM-generated wiki from local source material with Microsoft Agent Framework and GitHub Models.

The tool ingests files from `raw/`, generates wiki pages under `wiki/`, and supports lightweight retrieval and linting workflows.

## Commands

- `wiki init`
- `wiki ingest <file-or-folder>`
- `wiki ask "<question>"`
- `wiki lint`

Supported source types:

- `.md`
- `.txt`
- `.json`
- `.docx`

## Requirements

- .NET 10 SDK
- GitHub Models token

Environment variables:

- `GITHUB_TOKEN`
- `GITHUB_MODEL`
- `WIKI_ROOT`

Default model: `openai/gpt-4.1`

## Usage

Interactive mode:

```powershell
dotnet run --project .\src\WikiCli
```

In interactive mode, type `help` to list commands and `exit` or `quit` to leave.

Single-command mode:

```powershell
dotnet run --project .\src\WikiCli -- init
dotnet run --project .\src\WikiCli -- ingest .\some-meeting-notes.docx
dotnet run --project .\src\WikiCli -- ask "What decisions were captured in the notes?"
dotnet run --project .\src\WikiCli -- lint
```

## Layout

```text
raw/
  imported/
wiki/
  sources/
  topics/
  entities/
  analyses/
  index.md
  log.md
docs/
src/
tests/
```

## Notes

- `raw/` stores imported source material.
- `wiki/` stores generated wiki pages and analysis output.
- `wiki/index.md` and `wiki/log.md` track the generated structure and activity history.

## Tests

```powershell
dotnet test
```
