using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Flow.Launcher.Plugin;
using Shorty.Models;
using Xunit;

namespace Shorty.Tests;

public sealed class MainTests
{
    [Fact]
    public void ShortyResultIconUsesDisplaySizedAsset()
    {
        var field = typeof(Main).GetField("ShortyIcon", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);
        Assert.Equal("Images\\display\\Shorty_Flow_Round_Padded.png", Assert.IsType<string>(field.GetValue(null)));
    }

    [Fact]
    public void AnswerResultIconUsesPaddedDisplaySizedAsset()
    {
        var field = typeof(Main).GetField("AnswerIcon", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);
        Assert.Equal("Images\\display\\Shorty_Flow_Round_Padded.png", Assert.IsType<string>(field.GetValue(null)));
    }

    [Fact]
    public void PluginManifestUsesPaddedDisplayIcon()
    {
        var manifestPath = Path.Combine(AppContext.BaseDirectory, "plugin.json");

        Assert.True(File.Exists(manifestPath), $"Missing copied plugin manifest: {manifestPath}");
        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));

        Assert.Equal(
            "Images\\display\\Shorty_Flow_Round_Padded.png",
            document.RootElement.GetProperty("IcoPath").GetString());
    }

    [Fact]
    public void AnswerResultRowUsesPaddedUncroppedIcon()
    {
        var method = typeof(Main).GetMethod("RenderAnswerRows", BindingFlags.NonPublic | BindingFlags.Instance);
        var entry = new AnswerEntry
        {
            Id = "answer-1",
            Question = "Question",
            Text = "Answer",
            Preset = "default",
            Model = "test-model",
            Created = DateTimeOffset.UtcNow
        };

        Assert.NotNull(method);
        var rows = Assert.IsType<List<Result>>(method.Invoke(new Main(), [entry]));

        Assert.Equal("Answer", rows[0].Title);
        Assert.Equal("Images\\display\\Shorty_Flow_Round_Padded.png", rows[0].IcoPath);
        Assert.False(rows[0].RoundedIcon);
    }

    [Fact]
    public void LoadingFramesUseDisplaySizedNumberedAnimationIcons()
    {
        var field = typeof(Main).GetField("LoadingFrames", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);
        var frames = Assert.IsType<string[]>(field.GetValue(null));
        Assert.Equal(
            [
                "Images\\display\\Shorty_Ani1_Padded.png",
                "Images\\display\\Shorty_Ani2_Padded.png",
                "Images\\display\\Shorty_Ani3_Padded.png"
            ],
            frames);
    }

    [Fact]
    public void LoadingDotsDoNotIncludeBlankFrame()
    {
        var field = typeof(Main).GetField("LoadingDots", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);
        var dots = Assert.IsType<string[]>(field.GetValue(null));

        Assert.All(dots, dot => Assert.False(string.IsNullOrWhiteSpace(dot)));
    }

    [Theory]
    [InlineData("Shorty_Flow_Round_Padded.png")]
    [InlineData("Shorty_Ani1_Padded.png")]
    [InlineData("Shorty_Ani2_Padded.png")]
    [InlineData("Shorty_Ani3_Padded.png")]
    public void DisplaySizedIconAssetsAreIncluded(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Images", "display", fileName);

        Assert.True(File.Exists(path), $"Missing display-sized icon: {path}");
    }

    [Theory]
    [InlineData("copy.png")]
    [InlineData("Shorty_Eye.png")]
    [InlineData("other", "refresh.png")]
    public void ReferencedIconAssetsAreIncluded(params string[] pathParts)
    {
        var path = Path.Combine([AppContext.BaseDirectory, "Images", .. pathParts]);

        Assert.True(File.Exists(path), $"Missing referenced icon: {path}");
    }

    [Theory]
    [InlineData("Shorty_Ani1.png", "3C575062985AFEFE44048E449CF71327C017EA5AB6589ED67B6F2572E4A34781")]
    [InlineData("Shorty_Ani2.png", "66E4FD4F6D032257862CBC80B2C39A6AF9238680B2435DD29EC1AA04C2A0792F")]
    [InlineData("Shorty_Ani3.png", "34BD4E05DC019ECD886C20AB523610189E21D8177221899616B5EA9B7B8BEFBF")]
    public void LoadingFrameAssetsMatchUprightOriginals(string fileName, string expectedSha256)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Images", fileName);

        Assert.True(File.Exists(path), $"Missing animation frame: {path}");
        using var stream = File.OpenRead(path);
        var hash = Convert.ToHexString(SHA256.HashData(stream));

        Assert.Equal(expectedSha256, hash);
    }
}
