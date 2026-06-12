using System.Windows;

namespace Shorty;

public partial class PresetEditorWindow : Window
{
    public PresetEditorWindow(PresetEditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Owner = Application.Current?.MainWindow;
    }

    private PresetEditorViewModel ViewModel => (PresetEditorViewModel)DataContext;

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.Preset.Name))
        {
            MessageBox.Show(this, "Preset name is required.", "Shorty", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ViewModel.Preset.Name = ViewModel.Preset.Name.Trim();
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
