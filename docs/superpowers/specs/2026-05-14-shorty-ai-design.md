# Shorty AI — Flow Launcher Plugin Design

Date: 2026-05-14
Status: Draft (awaiting user review)

## Goal

Convert the existing AHK Commander plugin into **Shorty AI**, a Flow Launcher plugin that sends one-shot prompts to any OpenAI-compatible chat-completions endpoint and streams the response into a small popup window. No conversation memory; a configurable set of named system-prompt presets; minimum dependencies.

## Decisions made during brainstorming

| Decision | Choice | Reason |
|---|---|---|
| Provider strategy | OpenAI-compatible only (configurable `base_url`) | One code path covers OpenAI, OpenRouter, Groq, DeepSeek, LM Studio, Ollama. |
| Send trigger | Sentinel result + Enter | No `\|\|` ceremony, discoverable via Flow's standard interaction. |
| Response display | Borderless tkinter popup, streaming tokens | Flow's result list is a poor reading surface for paragraph output. Tk ships with Python — zero install. |
| Conversation memory | None (one-shot per query) | Matches the reference plugins; smallest surface area. |
| System prompt | Multiple named presets, switchable per query (`ai code …`, `ai short …`) | Power-user friendly without per-query ceremony. |
| Settings storage | Hybrid — Flow's `SettingsTemplate.yaml` for simple fields, `presets.json` for prompts | Flow's settings UI is poor for variable-length textareas. |
| HTTP client | `requests` only (no `openai` SDK) | OpenAI-compatible SSE is ~30 lines to parse; saves ~10MB of transitive deps. |
| Process model | Detached popup subprocess; main.py exits on dispatch | Simplest lifecycle; no daemon, no IPC. |
| Plugin identity | Fresh UUID `cf513e07-af2b-4c60-a09d-c8f25cbae1d8`, name "Shorty AI", keyword `ai` | Avoid colliding with the AHK Commander listing. |

## Repository layout

### Files to delete

- `ipc.py`
- `daemon-template/` (entire folder, including `lib/`)
- `Images/ahk.ico`, `Images/ahk.png`
- `AHK Commander-f47ac10b-58cc-4372-a567-0e02b2c3d479.json` (marketplace registration file at repo root)

### Files to rewrite

- `main.py` — Flow query handler; sentinel + admin commands; popup launcher
- `plugin.json` — fresh UUID, "Shorty AI", keyword `ai`
- `README.md` — install, settings, presets, troubleshooting
- `requirements.txt` — `pyflowlauncher`, `requests`

### Files to add

- `popup.py` — Tk window + streaming HTTP client
- `presets.py` — load/seed `presets.json`; preset matching against query
- `SettingsTemplate.yaml` — Flow's settings UI
- `Images/icon.png` and `Images/icon.ico` — replacement icon (placeholder OK initially)

### Files kept as-is

- `LICENSE`, `.gitignore`, `.vscode/`, `.github/workflows/`

### Final tree

```
Flow.Launcher.Plugin.Shorty/
├── main.py
├── popup.py
├── presets.py
├── plugin.json
├── SettingsTemplate.yaml
├── requirements.txt
├── README.md
├── LICENSE
└── Images/
    ├── icon.png
    └── icon.ico
```

## Component responsibilities

Strict separation of concerns so each module is testable in isolation.

### `main.py` (~80 lines)

- Imports: `pyflowlauncher`, `presets` (local), `subprocess`, `os`, `sys`.
- Does **not** import `requests` or `tkinter`.
- `query(q)` — strips input.
  - If `q` starts with `:`, dispatches to admin-only mode and returns just the matching admin row (e.g. `:presets`).
  - Otherwise calls `presets.match(q, default=settings.default_preset)` and returns the sentinel row plus the `:presets` admin row beneath it.
- `ask(preset_name, prompt)` — JsonRPC method invoked on Enter. Resolves `pythonw.exe`, builds the popup argv, spawns detached.
- `open_presets_file()` — opens `presets.json` in the OS default editor (`os.startfile`).

### `popup.py` (~200 lines)

- Argv: `popup.py <preset_name> <prompt> <settings_json_path>`.
- Loads `Settings.json` (api_key, base_url, default_model, request_timeout) and `presets.json` (system prompt + optional per-preset model override).
- Opens a 600×400 borderless-style Tk window:
  - Background `#0f0f0f`, text widget background `#121212` with 1px `#303030` border.
  - Header line: dim gray (`#a0a0a0`) prompt echo.
  - Body: monospace text widget, scrollable, white text on `#121212`.
  - Footer: three buttons — Copy / Regenerate / Close. Disabled until first token arrives (Copy) or stream completes (Regenerate).
- Worker thread issues `POST {base_url}/chat/completions` with `stream=true`.
- SSE parser pulls `choices[0].delta.content` from each `data: {…}` chunk and pushes to a `queue.Queue`.
- Tk mainloop drains the queue every 30ms via `root.after()`; appends to text widget; auto-scrolls.
- `[DONE]` sentinel or `finish_reason != null` triggers a `_DONE` marker that enables the Regenerate button.
- Esc / window-close destroys root, process exits.

### `presets.py` (~60 lines)

- Pure stdlib (`json`, `os`, `pathlib`).
- On first import, ensures `<plugin settings dir>/presets.json` exists; seeds with defaults if missing:
  ```json
  {
    "default":   { "system_prompt": "You are a concise, helpful assistant." },
    "short":     { "system_prompt": "Reply in one sentence. No preamble." },
    "code":      { "system_prompt": "Reply with code only, no commentary. Use markdown fences only if multiple files." },
    "translate": { "system_prompt": "Translate the user's input to English. Output the translation only." }
  }
  ```
- Each preset entry may also include `"model": "<override>"`.
- Public API:
  - `load() -> dict[str, Preset]`
  - `path() -> str`
  - `match(query: str, default: str = "default") -> tuple[str, str]` — splits on first whitespace; if first token matches a preset name (case-insensitive), returns `(preset_name, remainder)`; else returns `(default, query)`. The caller (main.py) passes `default_preset` from settings.

### `SettingsTemplate.yaml`

```yaml
body:
  - type: input
    attributes:
      name: api_key
      label: API key
      description: OpenAI-compatible bearer token. Stored in plain text in Flow's settings file.
  - type: input
    attributes:
      name: base_url
      label: Base URL
      description: OpenAI-compatible endpoint, no trailing slash.
      defaultValue: https://api.openai.com/v1
  - type: input
    attributes:
      name: default_model
      label: Default model
      defaultValue: gpt-4o-mini
  - type: input
    attributes:
      name: default_preset
      label: Default preset name
      description: Preset to use when no preset name is detected as the first word of the query.
      defaultValue: default
  - type: input
    attributes:
      name: request_timeout
      label: Request timeout (seconds)
      defaultValue: "60"
```

**Schema verification step (implementation task):** Flow Launcher's `SettingsTemplate.yaml` schema is owned by Flow itself, not pyflowlauncher. Before writing this file, the implementer must read at least one existing published Flow plugin's `SettingsTemplate.yaml` (e.g. from `Flow.Launcher.Plugin.ChatGPT` or any other Python plugin in Flow's marketplace repo) to confirm the widget names (`input` vs `textbox`, whether a `password` widget exists for the API key, the exact `attributes` keys). Adjust the YAML above to match the canonical schema before merging.

### `plugin.json`

```json
{
  "ID": "cf513e07-af2b-4c60-a09d-c8f25cbae1d8",
  "ActionKeyword": "ai",
  "Name": "Shorty AI",
  "Description": "One-shot AI prompts in a streaming popup, against any OpenAI-compatible endpoint",
  "Author": "TrueCrimeAudit",
  "Version": "0.1.0",
  "Language": "python",
  "Website": "https://github.com/TrueCrimeDev/Flow.Launcher.Plugin.Shorty",
  "ExecuteFileName": "main.py",
  "IcoPath": "Images\\icon.png"
}
```

## Data flow (one full query)

1. **User types `ai code regex for emails`.** Flow invokes `main.py query "code regex for emails"` per keystroke.
   - `presets.match` returns `("code", "regex for emails")`.
   - main.py returns: sentinel row `Ask [code]: regex for emails` / `Press Enter to send to gpt-4o-mini` with action `{"method": "ask", "parameters": ["code", "regex for emails"]}`, plus the admin row `ai :presets — Open presets.json`.
2. **User presses Enter.** Flow invokes `main.py ask "code" "regex for emails"`.
   - Resolves `pythonw.exe` from `sys.executable`, popup.py path from `__file__` sibling, settings path via `pyflowlauncher`.
   - `subprocess.Popen([pythonw, popup.py, "code", "regex for emails", settings_path], creationflags=DETACHED_PROCESS | CREATE_NO_WINDOW)`. Returns immediately.
3. **popup.py starts (~200ms cold).**
   - Reads settings + presets. Opens Tk window. Echoes the prompt.
   - Worker thread POSTs to `<base_url>/chat/completions` with `{model, stream: true, messages: [system, user]}`, `Authorization: Bearer <api_key>`.
4. **Streaming.** Worker iterates `response.iter_lines()`. For each `data: {…}` line, parses JSON, extracts `choices[0].delta.content`, pushes onto `queue.Queue`. Tk's `_drain_queue()` runs every 30ms, drains everything, appends to the text widget, auto-scrolls.
5. **Stream complete.** `data: [DONE]` (or `finish_reason != null`) triggers a `_DONE` queue marker. Drainer enables Copy/Regenerate buttons and stops re-scheduling.
6. **User actions.**
   - Copy → Tk clipboard with full response text; button label flashes "Copied!" for 1s.
   - Regenerate → clears text widget, re-spawns worker thread with same args.
   - Close / Esc / X → destroys root, process exits.

**Process boundaries:** main.py wall-clock is sub-50ms per Flow call. popup.py runs for the user's reading time. They share only argv and the settings file on disk — no sockets, no shared memory.

## Error handling

| # | Failure | Behavior |
|---|---|---|
| 1 | Empty `api_key` on popup launch | Popup opens with single message: *"No API key configured. Open Flow Launcher → Settings → Plugins → Shorty AI and paste your key."* Buttons: Close only. |
| 2 | HTTP transport error (DNS / refused / TLS / timeout) | Worker catches `requests.exceptions.RequestException`, pushes `f"Error: {type(e).__name__}: {e}"` and `_DONE`. Renders in `#DC3545`. Regenerate enabled. |
| 3 | HTTP non-2xx | Worker reads response body, extracts `error.message` if JSON, falls back to raw body. Renders as `Error {status}: {message}` in red. Regenerate enabled. |
| 4 | Malformed SSE chunk | `try/except` around `json.loads`. Log raw line to `popup.log` (before each write: if `os.path.getsize(log) > 1_000_000`, truncate to empty, then append). Skip chunk. Stream continues. |
| 5 | Settings.json missing/corrupt | popup.py wraps load in try/except. Same UX as #1, message points at settings UI. main.py never reads settings, so unaffected. |

**Logging:** one rotating log file `<plugin settings dir>/popup.log`, errors only, manual size check + truncate (no `logging.handlers`). main.py uses pyflowlauncher's default logging — nothing custom.

**Not handled** (these can't reasonably happen in production):
- `tkinter` import failure
- Malformed argv to popup.py (only main.py constructs it)
- Concurrent popups (each is its own process; no shared state to coordinate)
- Malformed `presets.json` (presets.py silently falls back to seeded defaults; bad path surfaces in main.py's sentinel subtitle so the user notices)

## Testing strategy

The hard rule from CLAUDE.md is "tests verify code correctness, not feature correctness." So unit-test the pure logic, smoke-test the popup manually.

### Unit tests (Jest-equivalent: pytest)

- `tests/test_presets.py`
  - Seeds defaults when `presets.json` missing.
  - Preserves user file when present.
  - `match("code regex for emails")` → `("code", "regex for emails")`.
  - `match("regex for emails")` → `("default", "regex for emails")`.
  - `match("CODE foo")` → `("code", "foo")` (case-insensitive).
  - `match("")` → `("default", "")`.
- `tests/test_main_query.py`
  - Mock `presets.match` and `subprocess.Popen`.
  - `query("code foo")` returns sentinel + `:presets` admin row, in that order.
  - `query("")` returns sentinel-with-empty-prompt (disabled, subtitle prompts user to type) + `:presets` admin row.
  - `query(":presets")` returns the open-presets-file row only (no sentinel).
  - `ask("code", "foo")` constructs the right argv and calls Popen with detached flags.
- `tests/test_sse_parser.py`
  - Pull the SSE chunk parser out of `popup.py` into a pure function `parse_chunk(line: str) -> str | None`.
  - Returns content for normal `data: {…}` line.
  - Returns `None` for `data: [DONE]`.
  - Returns `None` for empty/malformed line.
  - Returns `None` for chunks where `choices[0].delta` lacks `content`.

### Manual smoke test

A `docs/SMOKE_TEST.md` checklist:

1. Type `ai`, see admin rows. Type `ai foo`, see sentinel. Press Enter — popup opens.
2. Streaming visibly token-by-token against the real API.
3. Copy button copies full text. Regenerate clears + re-streams. Close exits.
4. Set bad API key in settings → popup shows red "Error 401: …" with retry available.
5. Disconnect network → popup shows red transport error.
6. Edit `presets.json` to add `pirate` preset → `ai pirate hello` uses it without restart.

CI (existing GitHub workflow) runs the unit tests on push.

## Implementation order

(For the writing-plans skill to flesh out into discrete tasks)

1. **Cleanup** — delete AHK files; commit.
2. **Skeleton** — write new `plugin.json`, empty `main.py` returning a placeholder result, empty `requirements.txt`. Verify Flow loads it.
3. **`presets.py` + tests** — fully tested before anyone calls it.
4. **`main.py` query/ask + tests** — sentinel rows render correctly; subprocess spawn is mocked.
5. **`popup.py` minimum** — opens window, accepts argv, renders prompt echo, fakes streaming with `time.sleep` + canned tokens. No real HTTP yet.
6. **`popup.py` real streaming** — extract pure `parse_chunk`; wire up `requests` worker; test parser; manual smoke against real API.
7. **`popup.py` polish** — error states (1-5), Copy/Regenerate buttons, dark theme styling, Esc binding.
8. **`SettingsTemplate.yaml`** — verify Flow's settings UI shows all five fields and writes them to Settings.json.
9. **README + smoke test doc** — install, settings, preset editing, troubleshooting.
10. **Replacement icon** — placeholder PNG/ICO; user picks final art later.

## Out of scope (explicit non-goals for v1)

- Conversation memory or threads.
- Multi-modal (images, audio).
- Function/tool calling.
- Web search.
- Markdown rendering in the popup.
- Syntax highlighting in the popup.
- Custom themes beyond the global dark palette.
- Streaming abort button (Close kills the process and tears the connection — sufficient for v1).
- Cost tracking / usage display.
- Keyboard shortcuts beyond Esc-to-close.
