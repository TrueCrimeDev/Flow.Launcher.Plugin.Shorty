# Shorty

Shorty is a C# Flow Launcher plugin that sends one-shot prompts to any OpenAI-compatible
chat API and renders the answer as Markdown inside Flow Launcher.

It doubles as the test plugin for the **markdown preview pane** work on
[`TrueCrimeDev/Flow.Launcher@markdown-preview-pane`](https://github.com/TrueCrimeDev/Flow.Launcher/tree/markdown-preview-pane),
exercising the new plugin APIs end to end:

- `Result.Preview` with `PreviewContentType.Markdown` (rendered answers, code highlighting) and `PreviewContentType.Hidden`
- `Result.RoundedIcon`
- `IPublicAPI.StartLoadingBar` / `StopLoadingBar`
- `IPublicAPI.LoadImageAsync` (preloading the loading-animation frames)

> **Note:** Shorty is built against those fork APIs and will not load on a stock
> Flow Launcher release. It is meant to be run against the fork's debug build.

## Layout

- `Shorty/` — plugin source and `plugin.json`
- `Shorty.Tests/` — xUnit test project

The repository root is intentionally not a loadable plugin; Flow loads the manifest at
`Shorty/plugin.json` from a published output directory.

## Requirements

- Windows, .NET 9 SDK
- The Flow.Launcher fork checked out **beside this repo** — `Shorty.csproj` references
  `Flow.Launcher.Plugin` from the sibling checkout:

```text
<your folder>/
├── Flow.Launcher/                  # TrueCrimeDev/Flow.Launcher @ markdown-preview-pane
└── Flow.Launcher.Plugin.Shorty/    # this repo
```

```powershell
git clone -b markdown-preview-pane https://github.com/TrueCrimeDev/Flow.Launcher.git
git clone https://github.com/TrueCrimeDev/Flow.Launcher.Plugin.Shorty.git
```

## Build, test, run

From the repo root:

```powershell
dotnet test .\Shorty.Tests\Shorty.Tests.csproj
dotnet publish .\Shorty\Shorty.csproj -c Debug -o ..\Flow.Launcher\Output\Debug\Plugins\Shorty
```

Then build and run the Flow Launcher debug build from the sibling checkout; it loads
Shorty from `Output\Debug\Plugins\Shorty`.

CI does the same two-repo layout on `windows-latest` — see
[`.github/workflows/ci.yml`](.github/workflows/ci.yml).

## Setup

1. Open Flow Launcher settings → Plugins → Shorty.
2. Set your API key, endpoint URL, and default model (any OpenAI-compatible provider).

## Usage

- `hey <prompt>` then Enter — the answer renders as Markdown in the preview pane.
- Enter on the answer row copies it; the context menu offers **Copy as Markdown**,
  **Save to file**, and **Open in editor**.
- `hey :presets` opens `presets.json` to define prompt presets (per-preset model,
  system prompt, and temperature).

## License

MIT
