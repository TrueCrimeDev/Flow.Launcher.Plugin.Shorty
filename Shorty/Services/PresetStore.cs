using System.Text.Json;
using System.IO;
using System.Text.Json.Serialization;
using Shorty.Models;

namespace Shorty.Services;

public sealed class PresetStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly object _gate = new();
    private Dictionary<string, Preset> _presets = new(StringComparer.OrdinalIgnoreCase);

    public PresetStore(string pluginDirectory)
    {
        PluginDirectory = pluginDirectory;
        PresetsPath = Path.Combine(pluginDirectory, "presets.json");
        Reload();
    }

    public string PluginDirectory { get; }

    public string PresetsPath { get; }

    public event EventHandler? PresetsChanged;

    public IReadOnlyList<Preset> GetAll()
    {
        lock (_gate)
        {
            return _presets.Values
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Select(p => p.Clone())
                .ToList();
        }
    }

    public Preset? Get(string name)
    {
        lock (_gate)
        {
            return _presets.TryGetValue(name, out var preset) ? preset.Clone() : null;
        }
    }

    public (string PresetName, string Prompt) Match(string query, string defaultPreset)
    {
        var q = (query ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(q))
        {
            return (defaultPreset, string.Empty);
        }

        var parts = q.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var first = parts[0];
        lock (_gate)
        {
            foreach (var preset in _presets.Values)
            {
                if (string.Equals(preset.Name, first, StringComparison.OrdinalIgnoreCase))
                {
                    return (preset.Name, parts.Length > 1 ? parts[1] : string.Empty);
                }
            }
        }

        return (defaultPreset, q);
    }

    public void Reload()
    {
        Directory.CreateDirectory(PluginDirectory);
        if (!File.Exists(PresetsPath))
        {
            WritePresets(DefaultPresets());
        }

        Dictionary<string, Preset> loaded;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(PresetsPath));
            loaded = ParsePresets(document.RootElement);
        }
        catch (JsonException)
        {
            loaded = DefaultPresets();
        }
        catch (IOException)
        {
            loaded = DefaultPresets();
        }

        if (loaded.Count == 0)
        {
            loaded = DefaultPresets();
        }

        lock (_gate)
        {
            _presets = loaded;
        }

        PresetsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Save(IEnumerable<Preset> presets)
    {
        var normalized = presets
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .Select(p => p.Clone())
            .GroupBy(p => p.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var preset = g.Last();
                preset.Name = g.Key;
                preset.Temperature = Math.Clamp(preset.Temperature, 0, 2);
                return preset;
            })
            .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

        if (normalized.Count == 0)
        {
            normalized = DefaultPresets();
        }

        WritePresets(normalized);

        lock (_gate)
        {
            _presets = normalized;
        }

        PresetsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void WritePresets(Dictionary<string, Preset> presets)
    {
        Directory.CreateDirectory(PluginDirectory);
        var payload = new PresetsFile
        {
            Presets = presets
                .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => new PresetFileEntry
                    {
                        System = kvp.Value.System,
                        Model = kvp.Value.Model,
                        Temperature = Math.Clamp(kvp.Value.Temperature, 0, 2)
                    },
                    StringComparer.OrdinalIgnoreCase)
        };
        File.WriteAllText(PresetsPath, JsonSerializer.Serialize(payload, JsonOptions));
    }

    private static Dictionary<string, Preset> ParsePresets(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, Preset>(StringComparer.OrdinalIgnoreCase);
        }

        if (root.TryGetProperty("presets", out var presetsElement) && presetsElement.ValueKind == JsonValueKind.Object)
        {
            return ParsePresetMap(presetsElement, newSchema: true);
        }

        return ParsePresetMap(root, newSchema: false);
    }

    private static Dictionary<string, Preset> ParsePresetMap(JsonElement map, bool newSchema)
    {
        var presets = new Dictionary<string, Preset>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in map.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var item = property.Value;
            var system = ReadString(item, "system");
            if (!newSchema && string.IsNullOrWhiteSpace(system))
            {
                system = ReadString(item, "system_prompt");
            }

            presets[property.Name] = new Preset
            {
                Name = property.Name,
                System = system,
                Model = ReadString(item, "model"),
                Temperature = ReadDouble(item, "temperature", 0.2)
            };
        }

        return presets;
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static double ReadDouble(JsonElement element, string propertyName, double fallback)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return fallback;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
        {
            return Math.Clamp(number, 0, 2);
        }

        return fallback;
    }

    private static Dictionary<string, Preset> DefaultPresets()
    {
        return new Dictionary<string, Preset>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = new Preset
            {
                Name = "default",
                System = "You are a concise, helpful assistant.",
                Temperature = 0.2
            },
            ["short"] = new Preset
            {
                Name = "short",
                System = "Reply in one sentence. No preamble.",
                Temperature = 0.2
            },
            ["code"] = new Preset
            {
                Name = "code",
                System = "Reply with code only, no commentary. Use markdown fences for code and always include the specific language tag.",
                Temperature = 0.1
            },
            ["translate"] = new Preset
            {
                Name = "translate",
                System = "Translate the user's input to English. Output the translation only.",
                Temperature = 0.2
            }
        };
    }

    private sealed class PresetsFile
    {
        [JsonPropertyName("presets")]
        public Dictionary<string, PresetFileEntry> Presets { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class PresetFileEntry
    {
        [JsonPropertyName("system")]
        public string System { get; init; } = "";

        [JsonPropertyName("model")]
        public string Model { get; init; } = "";

        [JsonPropertyName("temperature")]
        public double Temperature { get; init; } = 0.2;
    }
}
