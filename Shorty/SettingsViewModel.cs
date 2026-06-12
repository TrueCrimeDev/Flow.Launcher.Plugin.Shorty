using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shorty.Models;
using Shorty.Services;

namespace Shorty;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly Settings _settings;
    private readonly PresetStore _presetStore;
    private readonly LlmClient _llmClient;
    private readonly Action _saveSettings;
    private readonly Action _openLogFolder;
    private readonly Action<string> _logError;
    private Preset? _selectedPreset;
    private string _testStatus = "";
    private bool _showApiKey;

    public SettingsViewModel(
        Settings settings,
        PresetStore presetStore,
        LlmClient llmClient,
        Action saveSettings,
        Action openLogFolder,
        Action<string> logError)
    {
        _settings = settings;
        _presetStore = presetStore;
        _llmClient = llmClient;
        _saveSettings = saveSettings;
        _openLogFolder = openLogFolder;
        _logError = logError;

        ModelOptions =
        [
            "gpt-4o-mini",
            "gpt-4o",
            "gpt-4.1-mini",
            "gpt-4.1",
            "gpt-5.5",
            "anthropic/claude-3.5-sonnet",
            "openrouter/auto",
            "llama-3.1-8b-instant"
        ];

        Presets = new ObservableCollection<Preset>(_presetStore.GetAll());
        PresetNames = new ObservableCollection<string>(Presets.Select(p => p.Name));

        TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);
        AddPresetCommand = new RelayCommand(AddPreset);
        EditPresetCommand = new RelayCommand(EditSelectedPreset, () => SelectedPreset is not null);
        DeletePresetCommand = new RelayCommand(DeleteSelectedPreset, () => SelectedPreset is not null);
        DuplicatePresetCommand = new RelayCommand(DuplicateSelectedPreset, () => SelectedPreset is not null);
        OpenRepoCommand = new RelayCommand(OpenRepo);
        OpenLogFolderCommand = new RelayCommand(_openLogFolder);
    }

    public ObservableCollection<Preset> Presets { get; }

    public ObservableCollection<string> PresetNames { get; }

    public ObservableCollection<string> ModelOptions { get; }

    public IAsyncRelayCommand TestConnectionCommand { get; }

    public IRelayCommand AddPresetCommand { get; }

    public IRelayCommand EditPresetCommand { get; }

    public IRelayCommand DeletePresetCommand { get; }

    public IRelayCommand DuplicatePresetCommand { get; }

    public IRelayCommand OpenRepoCommand { get; }

    public IRelayCommand OpenLogFolderCommand { get; }

    public string ApiKey
    {
        get => _settings.ApiKey;
        set
        {
            if (_settings.ApiKey == value)
            {
                return;
            }

            _settings.ApiKey = value;
            OnPropertyChanged();
            SaveSettings();
        }
    }

    public string EndpointUrl
    {
        get => _settings.EndpointUrl;
        set
        {
            if (_settings.EndpointUrl == value)
            {
                return;
            }

            _settings.EndpointUrl = value;
            OnPropertyChanged();
            SaveSettings();
        }
    }

    public string DefaultModel
    {
        get => _settings.DefaultModel;
        set
        {
            if (_settings.DefaultModel == value)
            {
                return;
            }

            _settings.DefaultModel = value;
            OnPropertyChanged();
            SaveSettings();
        }
    }

    public string DefaultPreset
    {
        get => _settings.DefaultPreset;
        set
        {
            if (_settings.DefaultPreset == value)
            {
                return;
            }

            _settings.DefaultPreset = value;
            OnPropertyChanged();
            SaveSettings();
        }
    }

    public double CacheSizeValue
    {
        get => _settings.CacheSize;
        set
        {
            var size = Math.Clamp((int)Math.Round(value), 1, 200);
            if (_settings.CacheSize == size)
            {
                return;
            }

            _settings.CacheSize = size;
            OnPropertyChanged();
            SaveSettings();
        }
    }

    public bool ShowCharacterCount
    {
        get => _settings.ShowCharacterCount;
        set
        {
            if (_settings.ShowCharacterCount == value)
            {
                return;
            }

            _settings.ShowCharacterCount = value;
            OnPropertyChanged();
            SaveSettings();
        }
    }

    public bool ShowApiKey
    {
        get => _showApiKey;
        set => SetProperty(ref _showApiKey, value);
    }

    public Preset? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (SetProperty(ref _selectedPreset, value))
            {
                EditPresetCommand.NotifyCanExecuteChanged();
                DeletePresetCommand.NotifyCanExecuteChanged();
                DuplicatePresetCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string TestStatus
    {
        get => _testStatus;
        private set => SetProperty(ref _testStatus, value);
    }

    public string Version => "0.2.0";

    public string RepoUrl => "https://github.com/TrueCrimeDev/Flow.Launcher.Plugin.Shorty";

    private async Task TestConnectionAsync()
    {
        TestStatus = "Testing...";
        try
        {
            var preset = _presetStore.Get(DefaultPreset);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var response = await _llmClient.TestConnectionAsync(_settings, preset, timeout.Token).ConfigureAwait(false);
            TestStatus = string.IsNullOrWhiteSpace(response)
                ? "Connected. Provider returned an empty response."
                : $"Connected: {response.Trim()}";
        }
        catch (Exception ex)
        {
            _logError($"Test connection failed: {ex}");
            TestStatus = $"Failed: {ex.Message}";
        }
    }

    private void AddPreset()
    {
        var preset = new Preset
        {
            Name = UniquePresetName("preset"),
            System = "You are a concise, helpful assistant.",
            Model = "",
            Temperature = 0.2
        };

        if (EditPreset(preset))
        {
            Presets.Add(preset);
            SelectedPreset = preset;
            SavePresets();
        }
    }

    private void EditSelectedPreset()
    {
        if (SelectedPreset is null)
        {
            return;
        }

        var oldName = SelectedPreset.Name;
        var edited = SelectedPreset.Clone();
        if (!EditPreset(edited))
        {
            return;
        }

        SelectedPreset.Name = edited.Name;
        SelectedPreset.System = edited.System;
        SelectedPreset.Model = edited.Model;
        SelectedPreset.Temperature = edited.Temperature;
        if (string.Equals(DefaultPreset, oldName, StringComparison.OrdinalIgnoreCase))
        {
            DefaultPreset = edited.Name;
        }

        SavePresets();
    }

    private void DeleteSelectedPreset()
    {
        if (SelectedPreset is null)
        {
            return;
        }

        var deleted = SelectedPreset;
        Presets.Remove(deleted);
        SelectedPreset = Presets.FirstOrDefault();
        if (string.Equals(DefaultPreset, deleted.Name, StringComparison.OrdinalIgnoreCase))
        {
            DefaultPreset = SelectedPreset?.Name ?? "default";
        }

        SavePresets();
    }

    private void DuplicateSelectedPreset()
    {
        if (SelectedPreset is null)
        {
            return;
        }

        var copy = SelectedPreset.Clone();
        copy.Name = UniquePresetName($"{copy.Name}-copy");
        if (EditPreset(copy))
        {
            Presets.Add(copy);
            SelectedPreset = copy;
            SavePresets();
        }
    }

    private bool EditPreset(Preset preset)
    {
        var editor = new PresetEditorWindow(new PresetEditorViewModel(preset, ModelOptions));
        return editor.ShowDialog() == true;
    }

    private void SavePresets()
    {
        _presetStore.Save(Presets);
        RefreshPresetNames();
    }

    private void RefreshPresetNames()
    {
        PresetNames.Clear();
        foreach (var preset in Presets.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            PresetNames.Add(preset.Name);
        }

        OnPropertyChanged(nameof(PresetNames));
    }

    private string UniquePresetName(string baseName)
    {
        var cleanBase = string.IsNullOrWhiteSpace(baseName) ? "preset" : baseName.Trim();
        var existing = Presets.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!existing.Contains(cleanBase))
        {
            return cleanBase;
        }

        for (var index = 2; index < 1000; index++)
        {
            var candidate = $"{cleanBase}-{index}";
            if (!existing.Contains(candidate))
            {
                return candidate;
            }
        }

        return $"{cleanBase}-{Guid.NewGuid():N}"[..32];
    }

    private void OpenRepo()
    {
        Process.Start(new ProcessStartInfo(RepoUrl)
        {
            UseShellExecute = true
        });
    }

    private void SaveSettings()
    {
        _saveSettings();
    }
}

public sealed class PresetEditorViewModel : ObservableObject
{
    public PresetEditorViewModel(Preset preset, IEnumerable<string> modelOptions)
    {
        Preset = preset;
        ModelOptions = new ObservableCollection<string>(modelOptions);
    }

    public Preset Preset { get; }

    public ObservableCollection<string> ModelOptions { get; }
}
