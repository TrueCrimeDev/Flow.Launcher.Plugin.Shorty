using System.Text.Json;
using Xunit;

namespace Shorty.Tests;

public sealed class RepositoryLayoutTests
{
    [Fact]
    public void RepositoryRootIsNotALoadableFlowPlugin()
    {
        var root = FindRepositoryRoot();

        Assert.False(
            File.Exists(Path.Combine(root, "plugin.json")),
            "The repository root should not contain plugin.json; the active C# plugin manifest belongs under Shorty\\.");
    }

    [Fact]
    public void ActivePluginManifestIsCSharpProjectManifest()
    {
        var root = FindRepositoryRoot();
        var manifestPath = Path.Combine(root, "Shorty", "plugin.json");

        Assert.True(File.Exists(manifestPath), $"Missing active C# plugin manifest: {manifestPath}");

        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        Assert.Equal("csharp", document.RootElement.GetProperty("Language").GetString());
        Assert.Equal("Shorty.dll", document.RootElement.GetProperty("ExecuteFileName").GetString());
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) &&
                File.Exists(Path.Combine(directory.FullName, "Shorty", "Shorty.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Shorty repository root.");
    }
}
