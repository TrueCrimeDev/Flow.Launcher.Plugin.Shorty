using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Shorty;

public partial class SettingsView : UserControl
{
    private bool _syncingApiKey;

    public SettingsView(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        ApiKeyPasswordBox.Password = viewModel.ApiKey;
        ApiKeyTextBox.Text = viewModel.ApiKey;
        viewModel.PropertyChanged += ViewModelOnPropertyChanged;
    }

    private SettingsViewModel ViewModel => (SettingsViewModel)DataContext;

    private void ApiKeyPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_syncingApiKey)
        {
            return;
        }

        _syncingApiKey = true;
        ViewModel.ApiKey = ApiKeyPasswordBox.Password;
        ApiKeyTextBox.Text = ApiKeyPasswordBox.Password;
        _syncingApiKey = false;
    }

    private void ApiKeyTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncingApiKey)
        {
            return;
        }

        _syncingApiKey = true;
        ViewModel.ApiKey = ApiKeyTextBox.Text;
        ApiKeyPasswordBox.Password = ApiKeyTextBox.Text;
        _syncingApiKey = false;
    }

    private void ShowApiKey_OnChanged(object sender, RoutedEventArgs e)
    {
        UpdateApiKeyVisibility();
    }

    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.ShowApiKey))
        {
            UpdateApiKeyVisibility();
        }
    }

    private void UpdateApiKeyVisibility()
    {
        var show = ViewModel.ShowApiKey;
        ApiKeyTextBox.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        ApiKeyPasswordBox.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
    }
}
