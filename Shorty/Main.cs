using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Flow.Launcher.Plugin;
using Microsoft.Win32;
using Shorty.Models;
using Shorty.Services;

namespace Shorty;

public sealed class Main : IPlugin, ISettingProvider, IContextMenu, IPluginI18n
{
    private const string ShortyIcon = "Images\\display\\Shorty_Flow_Round_Padded.png";
    private const string AnswerIcon = "Images\\display\\Shorty_Flow_Round_Padded.png";
    private const string RefreshIcon = "Images\\other\\refresh.png";
    private const string PluginName = "Shorty";
    private static string CopyIcon => Path.Combine(AppContext.BaseDirectory, "Images", "copy.png");

    private static readonly char[] Base36 = "0123456789abcdefghijklmnopqrstuvwxyz".ToCharArray();

    private PluginInitContext? _context;
    private Settings _settings = new();
    private PresetStore? _presetStore;
    private AnswerCache _answerCache = new();
    private readonly LlmClient _llmClient = new();
    private (string Search, string Id)? _lastAsk;
    private (string Search, string Prompt, string Preset)? _inFlight;
    private System.Threading.Timer? _loadingTimer;
    private int _loadingFrame;
    private static readonly string[] LoadingDots = { ".", "..", "...", "..", "." };
    private static readonly string[] LoadingFrames =
    {
        "Images\\display\\Shorty_Ani1_Padded.png",
        "Images\\display\\Shorty_Ani2_Padded.png",
        "Images\\display\\Shorty_Ani3_Padded.png"
    };

    public void Init(PluginInitContext context)
    {
        _context = context;
        _settings = context.API.LoadSettingJsonStorage<Settings>();
        _answerCache = new AnswerCache(_settings.CacheSize);
        _settings.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(Settings.CacheSize))
            {
                _answerCache.SetCapacity(_settings.CacheSize);
            }
        };

        var pluginDirectory = context.CurrentPluginMetadata.PluginDirectory;
        if (string.IsNullOrWhiteSpace(pluginDirectory))
        {
            pluginDirectory = Path.GetDirectoryName(typeof(Main).Assembly.Location) ?? AppContext.BaseDirectory;
        }

        _presetStore = new PresetStore(pluginDirectory);
        LogInfo("Initialized C# plugin skeleton and services");
    }

    public List<Result> Query(Query query)
    {
        try
        {
            return QueryInternal(query);
        }
        catch (Exception ex)
        {
            LogError($"Query failed: {ex}");
            return
            [
                ErrorRow("Shorty error", ex.Message)
            ];
        }
    }

    public System.Windows.Controls.Control CreateSettingPanel()
    {
        EnsureInitialized();
        return new SettingsView(new SettingsViewModel(
            _settings,
            _presetStore!,
            _llmClient,
            SaveSettings,
            OpenLogFolder,
            LogError));
    }

    public List<Result> LoadContextMenus(Result selectedResult)
    {
        if (selectedResult.ContextData is not AnswerEntry entry)
        {
            return [];
        }

        return
        [
            new Result
            {
                Title = "Copy as Markdown",
                SubTitle = entry.Question,
                IcoPath = CopyIcon,
                Action = _ =>
                {
                    _context?.API.CopyToClipboard(FormatMarkdown(entry), directCopy: false, showDefaultNotification: true);
                    return false;
                }
            },
            new Result
            {
                Title = "Save to file",
                SubTitle = "Save answer as Markdown",
                IcoPath = CopyIcon,
                Action = _ => SaveAnswerToFile(entry)
            },
            new Result
            {
                Title = "Open in editor",
                SubTitle = "Open a temporary Markdown file",
                IcoPath = RefreshIcon,
                Action = _ => OpenAnswerInEditor(entry)
            }
        ];
    }

    public string GetTranslatedPluginTitle() => "Shorty";

    public string GetTranslatedPluginDescription() =>
        "One-shot AI prompts rendered inline in Flow Launcher.";

    public void OnCultureInfoChanged(CultureInfo cultureInfo)
    {
    }

    private List<Result> QueryInternal(Query query)
    {
        EnsureInitialized();

        var search = (query.Search ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(search))
        {
            _lastAsk = null;
            return [PlaceholderRow()];
        }

        if (search.StartsWith(':'))
        {
            return QueryCommand(search);
        }

        if (_inFlight is { } pending
            && string.Equals(pending.Search, search, StringComparison.Ordinal))
        {
            return [LoadingRow(pending.Preset, pending.Prompt)];
        }

        if (_lastAsk is { } last
            && string.Equals(last.Search, search, StringComparison.Ordinal)
            && _answerCache.TryGet(last.Id, out var cached))
        {
            return RenderAnswerRows(cached);
        }

        var (presetName, prompt) = _presetStore!.Match(search, _settings.DefaultPreset);
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return [PlaceholderRow(presetName)];
        }

        return
        [
            new Result
            {
                Title = $"Ask [{presetName}]: {prompt}",
                SubTitle = $"Press Enter to send to {EffectiveModel(presetName)}",
                IcoPath = ShortyIcon,
                Score = 100,
                RoundedIcon = true,
                Preview = new Result.PreviewInfo { ContentType = PreviewContentType.Hidden },
                AsyncAction = async _ =>
                {
                    await AskAndNavigateAsync(presetName, prompt, search, CancellationToken.None).ConfigureAwait(false);
                    return false;
                }
            }
        ];
    }

    private List<Result> QueryCommand(string search)
    {
        var command = search[1..].Trim();
        var parts = command.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var name = parts.Length > 0 ? parts[0].ToLowerInvariant() : "";
        var argument = parts.Length > 1 ? parts[1] : "";

        return name switch
        {
            "answer" => AnswerRows(argument),
            "presets" => [OpenPresetsRow()],
            _ => [ErrorRow($"Unknown admin command: {search}", "Try :presets")]
        };
    }

    private List<Result> AnswerRows(string id)
    {
        if (!string.IsNullOrWhiteSpace(id) && _answerCache.TryGet(id, out var entry))
        {
            return RenderAnswerRows(entry);
        }

        return [ExpiredRow(id)];
    }

    private List<Result> RenderAnswerRows(AnswerEntry entry)
    {
        var preview = new Result.PreviewInfo
        {
            IsMedia = false,
            ContentType = PreviewContentType.Markdown,
            Description = entry.Text
        };

        var subtitle = entry.IsError
            ? entry.ErrorTitle
            : entry.Question;
        var chars = entry.Text.Length;
        var copySubtitle = _settings.ShowCharacterCount
            ? $"{chars} chars"
            : entry.Model;

        return
        [
            new Result
            {
                Title = "Answer",
                SubTitle = subtitle,
                IcoPath = entry.IsError ? RefreshIcon : AnswerIcon,
                Score = 100,
                RoundedIcon = false,
                CopyText = entry.Text,
                Preview = preview,
                ContextData = entry,
                Action = _ =>
                {
                    _context?.API.CopyToClipboard(entry.Text, directCopy: false, showDefaultNotification: true);
                    return false;
                }
            },
            new Result
            {
                Title = "Copy",
                SubTitle = copySubtitle,
                IcoPath = CopyIcon,
                Score = 90,
                RoundedIcon = true,
                CopyText = entry.Text,
                Preview = preview,
                ContextData = entry,
                Action = _ =>
                {
                    _context?.API.CopyToClipboard(entry.Text, directCopy: false, showDefaultNotification: true);
                    return true;
                }
            }
        ];
    }

    private Result PlaceholderRow(string? presetName = null)
    {
        var preset = string.IsNullOrWhiteSpace(presetName) ? _settings.DefaultPreset : presetName;
        return new Result
        {
            Title = "The All Seeing Eye is waiting for you",
            SubTitle = $"Type a prompt then press Enter   [{preset}]",
            IcoPath = ShortyIcon,
            Score = 100,
            RoundedIcon = true,
            Preview = new Result.PreviewInfo
            {
                IsMedia = true,
                PreviewImagePath = "Images\\Shorty_Eye.png"
            },
            Action = _ => false
        };
    }

    private Result LoadingRow(string presetName, string prompt)
    {
        var dots = LoadingDots[((_loadingFrame % LoadingDots.Length) + LoadingDots.Length) % LoadingDots.Length];
        var frame = LoadingFrames[((_loadingFrame % LoadingFrames.Length) + LoadingFrames.Length) % LoadingFrames.Length];
        return new Result
        {
            Title = $"Answer{dots}",
            SubTitle = $"{prompt}{dots}",
            IcoPath = frame,
            Score = 100,
            RoundedIcon = true,
            Preview = new Result.PreviewInfo { ContentType = PreviewContentType.Hidden },
            Action = _ => false
        };
    }

    private Result OpenPresetsRow()
    {
        return new Result
        {
            Title = "Open presets.json",
            SubTitle = _presetStore!.PresetsPath,
            IcoPath = RefreshIcon,
            Score = 100,
            RoundedIcon = true,
            Action = _ =>
            {
                OpenFile(_presetStore!.PresetsPath);
                return true;
            }
        };
    }

    private Result ExpiredRow(string id)
    {
        AnswerRequest? request = null;
        var canAskAgain = !string.IsNullOrWhiteSpace(id) && _answerCache.TryGetExpiredRequest(id, out request);
        return new Result
        {
            Title = "Answer expired",
            SubTitle = "Press Enter to ask again",
            IcoPath = RefreshIcon,
            Score = 100,
            RoundedIcon = true,
            AsyncAction = async _ =>
            {
                if (!canAskAgain)
                {
                    return false;
                }

                await AskAndNavigateAsync(request!.Preset, request.Question, request.Question, CancellationToken.None).ConfigureAwait(false);
                return false;
            }
        };
    }

    private Result ErrorRow(string title, string detail)
    {
        return new Result
        {
            Title = title,
            SubTitle = detail,
            IcoPath = RefreshIcon,
            Score = 100,
            RoundedIcon = true,
            Preview = new Result.PreviewInfo
            {
                IsMedia = false,
                ContentType = PreviewContentType.Markdown,
                Description = detail
            },
            Action = _ => false
        };
    }

    private async Task AskAndNavigateAsync(string presetName, string prompt, string originalSearch, CancellationToken cancellationToken)
    {
        EnsureInitialized();
        var id = NewShortId();
        var preset = _presetStore!.Get(presetName);
        var model = string.IsNullOrWhiteSpace(preset?.Model) ? _settings.DefaultModel : preset!.Model;

        _inFlight = (originalSearch, prompt, presetName);
        _context?.API.StartLoadingBar();
        await PreloadLoadingIconsAsync().ConfigureAwait(false);
        _loadingFrame = 0;
        _loadingTimer?.Dispose();
        _loadingTimer = new System.Threading.Timer(_ =>
        {
            _loadingFrame++;
            _context?.API.ChangeQuery($"hey {originalSearch}", requery: true);
        }, null, 250, 250);
        _context?.API.ChangeQuery($"hey {originalSearch}", requery: true);

        try
        {
            LogInfo($"Asking preset={presetName} model={model}");
            var response = await _llmClient.AskAsync(_settings, preset, prompt, cancellationToken).ConfigureAwait(false);
            _answerCache.Set(new AnswerEntry
            {
                Id = id,
                Question = prompt,
                Text = response.Text,
                Preset = presetName,
                Model = response.Model,
                Created = DateTimeOffset.UtcNow
            });
        }
        catch (LlmClientException ex)
        {
            LogError($"{ex.Title}: {ex.Detail}");
            _answerCache.Set(new AnswerEntry
            {
                Id = id,
                Question = prompt,
                Text = ex.Detail,
                Preset = presetName,
                Model = model,
                Created = DateTimeOffset.UtcNow,
                IsError = true,
                ErrorTitle = ex.Title
            });
        }
        catch (Exception ex)
        {
            LogError($"Ask failed: {ex}");
            _answerCache.Set(new AnswerEntry
            {
                Id = id,
                Question = prompt,
                Text = ex.Message,
                Preset = presetName,
                Model = model,
                Created = DateTimeOffset.UtcNow,
                IsError = true,
                ErrorTitle = "Shorty error"
            });
        }

        _loadingTimer?.Dispose();
        _loadingTimer = null;
        _inFlight = null;
        _context?.API.StopLoadingBar();
        _lastAsk = (originalSearch, id);
        _context?.API.ChangeQuery($"hey {originalSearch}", requery: true);
    }

    private async Task PreloadLoadingIconsAsync()
    {
        var api = _context?.API;
        if (api is null)
        {
            return;
        }

        var pluginDirectory = _context!.CurrentPluginMetadata.PluginDirectory;
        if (string.IsNullOrWhiteSpace(pluginDirectory))
        {
            pluginDirectory = Path.GetDirectoryName(typeof(Main).Assembly.Location) ?? AppContext.BaseDirectory;
        }

        foreach (var iconPath in LoadingFrames)
        {
            var absolutePath = Path.IsPathRooted(iconPath)
                ? iconPath
                : Path.Combine(pluginDirectory, iconPath);

            await api.LoadImageAsync(absolutePath).ConfigureAwait(false);
        }
    }

    private string EffectiveModel(string presetName)
    {
        var preset = _presetStore?.Get(presetName);
        return string.IsNullOrWhiteSpace(preset?.Model) ? _settings.DefaultModel : preset!.Model;
    }

    private static string NewShortId()
    {
        Span<char> id = stackalloc char[8];
        for (var index = 0; index < id.Length; index++)
        {
            id[index] = Base36[RandomNumberGenerator.GetInt32(Base36.Length)];
        }

        return new string(id);
    }

    private static string FormatMarkdown(AnswerEntry entry)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Shorty Answer");
        builder.AppendLine();
        builder.AppendLine($"**Preset:** {entry.Preset}");
        builder.AppendLine($"**Model:** {entry.Model}");
        builder.AppendLine($"**Question:** {entry.Question}");
        builder.AppendLine();
        builder.AppendLine(entry.Text);
        return builder.ToString();
    }

    private bool SaveAnswerToFile(AnswerEntry entry)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save Shorty answer",
            FileName = $"shorty-{entry.Id}.md",
            DefaultExt = ".md",
            Filter = "Markdown (*.md)|*.md|Text (*.txt)|*.txt|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        File.WriteAllText(dialog.FileName, FormatMarkdown(entry), Encoding.UTF8);
        _context?.API.ShowMsg("Shorty", $"Saved {dialog.FileName}", ShortyIcon);
        return true;
    }

    private bool OpenAnswerInEditor(AnswerEntry entry)
    {
        var directory = Path.Combine(Path.GetTempPath(), "Shorty");
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, $"shorty-{entry.Id}.md");
        File.WriteAllText(file, FormatMarkdown(entry), Encoding.UTF8);
        OpenFile(file);
        return true;
    }

    private static void OpenFile(string path)
    {
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "");
        }

        Process.Start(new ProcessStartInfo(path)
        {
            UseShellExecute = true
        });
    }

    private void OpenLogFolder()
    {
        var logFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FlowLauncher",
            "Plugins",
            "Logs");
        Directory.CreateDirectory(logFolder);
        _context?.API.OpenDirectory(logFolder, "Shorty.log");
    }

    private void SaveSettings()
    {
        _context?.API.SaveSettingJsonStorage<Settings>();
    }

    private void EnsureInitialized()
    {
        if (_context is null || _presetStore is null)
        {
            throw new InvalidOperationException("Shorty has not been initialized by Flow Launcher.");
        }
    }

    private void LogInfo(string message)
    {
        _context?.API.LogInfo(nameof(Shorty), message, nameof(Main));
    }

    private void LogError(string message)
    {
        _context?.API.LogError(nameof(Shorty), message, nameof(Main));
    }
}
