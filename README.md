# Shorty

Shorty is a C# Flow Launcher plugin for sending one-shot prompts to an OpenAI-compatible chat API and rendering the answer inside Flow Launcher.

## Current layout

- `Shorty/` - active C# plugin source and `plugin.json`
- `Shorty.Tests/` - C# test project

The repository root is intentionally not a loadable Flow Launcher plugin. Flow should load the C# manifest at `Shorty/plugin.json`, and development builds should publish that project into Flow's plugin directory.

## Development

```powershell
dotnet test .\Shorty.Tests\Shorty.Tests.csproj --no-restore
dotnet publish .\Shorty\Shorty.csproj -c Debug -o ..\Flow.Launcher\Output\Debug\Plugins\Shorty --no-restore
```

The active debug Flow Launcher instance loads Shorty from:

```text
C:\Users\uphol\Documents\Design\Coding\Flow.Launcher\Output\Debug\Plugins\Shorty
```

## Setup

1. Open Flow Launcher settings.
2. Open Plugins -> Shorty.
3. Set your API key, base URL, default model, and preset options.

Shorty also supports environment/config fallback values in the plugin settings layer for OpenAI-compatible providers.

## License

MIT
