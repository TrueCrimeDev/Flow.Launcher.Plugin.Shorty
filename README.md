# Shorty

A [Flow Launcher](https://www.flowlauncher.com/) plugin that sends one-shot prompts to any OpenAI-compatible chat API and streams the response into a small popup window.

Type `hey`, then your prompt. Press Enter. A popup opens and the response streams in.

## Install

From Flow Launcher (once published):

```
pm install Shorty
```

Or clone this repo into `%APPDATA%\FlowLauncher\Plugins\` and restart Flow.

## Setup

1. Open Flow Launcher → Settings → Plugins → Shorty.
2. Paste your API key (OpenAI, OpenRouter, Groq, DeepSeek, LM Studio, Ollama — anything OpenAI-compatible).
3. (Optional) Change the base URL, default model, default preset.

| Setting | Default | Notes |
|---|---|---|
| `api_key` | *(empty)* | Bearer token. Plain text in Flow's settings file. |
| `base_url` | `https://api.openai.com/v1` | No trailing slash. |
| `default_model` | `gpt-5.5-instant` | Per-preset overrides supported (see Presets). |
| `default_preset` | `default` | Used when no preset name is the first word of your query. |
| `request_timeout` | `60` | Seconds. |

## Presets

A preset is a named system prompt. Trigger one by typing its name as the first word of your query:

- `hey code regex for matching emails` → uses the `code` preset.
- `hey short summarize this article` → uses the `short` preset.
- `hey what time is it in Tokyo` → uses the default preset (no match on first word).

Edit your presets by typing `hey :presets` and pressing Enter. The file opens in your default `.json` editor.

```json
{
  "default":   { "system_prompt": "You are a concise, helpful assistant." },
  "short":     { "system_prompt": "Reply in one sentence. No preamble." },
  "code":      { "system_prompt": "Reply with code only, no commentary." },
  "translate": { "system_prompt": "Translate the user's input to English." },
  "pirate":    { "system_prompt": "Reply as a pirate.", "model": "gpt-4o" }
}
```

Each preset entry may set `"model": "..."` to override the default model per-preset.

## Popup window

- **Esc** or window close button closes it.
- **Copy** copies the full response to your clipboard.
- **Regenerate** re-runs the same prompt.

## Troubleshooting

- **"No API key configured"** — paste your key in Flow's settings UI.
- **Streaming hangs / never starts** — check `%APPDATA%\FlowLauncher\Settings\Plugins\Shorty\popup.log` for the raw error.
- **Wrong model errors (404)** — confirm the model name matches what your provider accepts (e.g. OpenRouter wants `anthropic/claude-3-haiku`, not `claude-3-haiku`).

## Credits

- [pyflowlauncher](https://github.com/garulf/pyflowlauncher) — Python plugin runtime
- Inspired by [ShamanicArts/Flow.Launcher.Plugin.AI-Assistant](https://github.com/ShamanicArts/Flow.Launcher.Plugin.AI-Assistant), [MichielvanBeers/Flow.Launcher.Plugin.ChatGPT](https://github.com/MichielvanBeers/Flow.Launcher.Plugin.ChatGPT), and [BowieHe/ask-ai-plugin](https://github.com/BowieHe/ask-ai-plugin)

## License

MIT
