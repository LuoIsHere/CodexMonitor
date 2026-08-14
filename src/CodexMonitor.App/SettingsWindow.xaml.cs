using System.Windows;
using LuoIsHere.CodexMonitor.App.ViewModels;
using LuoIsHere.CodexMonitor.Infrastructure.Settings;

namespace LuoIsHere.CodexMonitor.App;

public partial class SettingsWindow : Window
{
    private readonly SettingsWindowViewModel _viewModel;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _viewModel = new SettingsWindowViewModel(settings);
        DataContext = _viewModel;
    }

    public AppSettings? SavedSettings { get; private set; }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        SavedSettings = _viewModel.CreateSettings();
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
        => DialogResult = false;

    private void OnCloseClick(object sender, RoutedEventArgs e)
        => Close();

    private void OnWindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            DialogResult = false;
        }
    }
}
