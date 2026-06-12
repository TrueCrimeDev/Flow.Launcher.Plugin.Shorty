using System.Text.Json;
using Shorty.Models;
using Shorty.Services;
using Xunit;

namespace Shorty.Tests;

public sealed class PresetStoreTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "shorty-tests", Guid.NewGuid().ToString("N"));

    public PresetStoreTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void LoadSeedsNewSchemaWhenFileIsMissing()
    {
        var store = new PresetStore(_tempDir);

        var presets = store.GetAll();

        Assert.Contains(presets, p => p.Name == "default" && p.System.Contains("concise"));
        Assert.True(File.Exists(Path.Combine(_tempDir, "presets.json")));
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(_tempDir, "presets.json")));
        Assert.True(json.RootElement.TryGetProperty("presets", out _));
    }

    [Fact]
    public void LoadReadsNamedPresetsFromRequestedSchema()
    {
        File.WriteAllText(Path.Combine(_tempDir, "presets.json"), """
        {
          "presets": {
            "code": {
              "system": "Return code only.",
              "model": "gpt-test",
              "temperature": 0.25
            }
          }
        }
        """);
        var store = new PresetStore(_tempDir);

        var preset = store.Get("code");

        Assert.NotNull(preset);
        Assert.Equal("code", preset.Name);
        Assert.Equal("Return code only.", preset.System);
        Assert.Equal("gpt-test", preset.Model);
        Assert.Equal(0.25, preset.Temperature);
    }

    [Fact]
    public void MatchUsesFirstTokenCaseInsensitiveAndReturnsRemainder()
    {
        File.WriteAllText(Path.Combine(_tempDir, "presets.json"), """
        {
          "presets": {
            "code": { "system": "Return code only." },
            "short": { "system": "One sentence." }
          }
        }
        """);
        var store = new PresetStore(_tempDir);

        var match = store.Match("CODE regex for emails", "short");

        Assert.Equal("code", match.PresetName);
        Assert.Equal("regex for emails", match.Prompt);
    }

    [Fact]
    public void LoadAcceptsLegacyTopLevelSystemPromptShape()
    {
        File.WriteAllText(Path.Combine(_tempDir, "presets.json"), """
        {
          "legacy": {
            "system_prompt": "Legacy prompt",
            "model": "gpt-legacy"
          }
        }
        """);
        var store = new PresetStore(_tempDir);

        var preset = store.Get("legacy");

        Assert.NotNull(preset);
        Assert.Equal("Legacy prompt", preset.System);
        Assert.Equal("gpt-legacy", preset.Model);
    }
}
