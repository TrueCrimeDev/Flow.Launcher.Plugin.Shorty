using Flow.Launcher.Plugin;

namespace Shorty;

public sealed class Settings : BaseModel
{
    private string _apiKey = "";
    private string _endpointUrl = "https://api.openai.com/v1/chat/completions";
    private string _defaultModel = "gpt-5.5";
    private string _defaultPreset = "default";
    private int _cacheSize = 50;
    private bool _showCharacterCount = true;
    private int _requestTimeoutSeconds = 60;

    public string ApiKey
    {
        get => _apiKey;
        set => SetValue(ref _apiKey, value ?? string.Empty);
    }

    public string EndpointUrl
    {
        get => string.IsNullOrWhiteSpace(_endpointUrl)
            ? "https://api.openai.com/v1/chat/completions"
            : _endpointUrl;
        set => SetValue(ref _endpointUrl, string.IsNullOrWhiteSpace(value)
            ? "https://api.openai.com/v1/chat/completions"
            : value.Trim());
    }

    public string DefaultModel
    {
        get => string.IsNullOrWhiteSpace(_defaultModel) ? "gpt-5.5" : _defaultModel;
        set => SetValue(ref _defaultModel, string.IsNullOrWhiteSpace(value) ? "gpt-5.5" : value.Trim());
    }

    public string DefaultPreset
    {
        get => string.IsNullOrWhiteSpace(_defaultPreset) ? "default" : _defaultPreset;
        set => SetValue(ref _defaultPreset, string.IsNullOrWhiteSpace(value) ? "default" : value.Trim());
    }

    public int CacheSize
    {
        get => Math.Clamp(_cacheSize, 1, 200);
        set => SetValue(ref _cacheSize, Math.Clamp(value, 1, 200));
    }

    public bool ShowCharacterCount
    {
        get => _showCharacterCount;
        set => SetValue(ref _showCharacterCount, value);
    }

    public int RequestTimeoutSeconds
    {
        get => Math.Clamp(_requestTimeoutSeconds, 5, 300);
        set => SetValue(ref _requestTimeoutSeconds, Math.Clamp(value, 5, 300));
    }

    private void SetValue<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }
}
