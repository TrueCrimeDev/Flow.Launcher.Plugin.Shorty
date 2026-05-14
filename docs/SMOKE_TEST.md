# Shorty AI Manual Smoke Test

Run this before any release. Each item should pass with no surprises.

## Prerequisites

- Plugin folder at `%APPDATA%\FlowLauncher\Plugins\Flow.Launcher.Plugin.Shorty\` (or symlinked there)
- Flow Launcher restarted after install
- API key configured in Flow Launcher settings
- Network connectivity

## Checklist

### Query handler

- [ ] Type `hey` (with no prompt) — see one disabled sentinel + the `:presets` row.
- [ ] Type `hey hello` — sentinel reads `Ask [default]: hello`. Press Enter — popup opens.
- [ ] Type `hey code regex for matching dates` — sentinel reads `Ask [code]: regex for matching dates`. Press Enter — popup uses the `code` preset's system prompt.
- [ ] Type `hey :presets` — only the admin row appears. Press Enter — `presets.json` opens in your default JSON editor.
- [ ] Type `hey :nope` — single help row appears.

### Popup window

- [ ] Window opens within ~500ms of pressing Enter.
- [ ] Streaming visibly token-by-token (not blocked until full response).
- [ ] Copy button enables on stream complete; clicking it puts the full response on the clipboard; label flashes "Copied!" for 1s then reverts.
- [ ] Regenerate clears the body and re-streams the same prompt + preset.
- [ ] Close button, window X button, and Esc all close the window cleanly.

### Error states

- [ ] Clear the API key in settings, run a query — popup shows red "No API key configured…".
- [ ] Set an obviously-bad API key (`sk-bad`), run a query — popup shows red "Error 401: …".
- [ ] Set an unreachable base URL (`https://localhost:9999/v1`), run a query — popup shows red transport error within ~60s.
- [ ] Edit `presets.json` to add a `pirate` preset, then run `hey pirate hello` immediately (no Flow restart) — uses the new preset.
- [ ] Corrupt `presets.json` with a stray `{`, then run `hey code hello` — falls back to `default` preset, no crash.

### Logging

- [ ] After triggering a transport error, `popup.log` exists and contains the exception line.
- [ ] Pre-fill `popup.log` with 1.1MB of garbage, trigger another error — file is truncated to just the new line.
