using System.Windows;
using LuoIsHere.CodexMonitor.App.ViewModels;
using LuoIsHere.CodexMonitor.Infrastructure.Settings;
using LuoIsHere.CodexMonitor.Infrastructure.Startup;

namespace LuoIsHere.CodexMonitor.App;

public partial class SettingsWindow : Window
{
    private readonly SettingsWindowViewModel _viewModel;
    private readonly UserStartupService _startup;
    private readonly StartupSettingsSession _session;
    private bool _saving;
    private TaskCompletionSource? _saveFinished;

    public SettingsWindow(AppSettings settings, UserStartupService startup, Func<AppSettings, Task> saveSettings)
    {
        InitializeComponent();
        _viewModel = new SettingsWindowViewModel(settings);
        _startup = startup;
        _session = new StartupSettingsSession(settings, startup, saveSettings);
        _viewModel.StartupStatus = startup.ReadStatus();
        DataContext = _viewModel;
        Closing += (_, e) => e.Cancel = _saving;
    }

    public Task WaitForSaveAsync() => _saveFinished?.Task ?? Task.CompletedTask;

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (_saving)
        {
            return;
        }

        _saving = true;
        _saveFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IsEnabled = false;
        try
        {
            var result = await _session.SaveAsync(_viewModel.CreateSettings(), _viewModel.RegisterCurrentPath);
            if (result.StartupOperationCompleted)
            {
                _viewModel.RegisterCurrentPath = false;
            }
            _viewModel.StartupStatus = _startup.ReadStatus();
            _viewModel.SaveError = result.Error ?? "";
            if (result.Success)
            {
                _saving = false;
                DialogResult = true;
            }
        }
        finally
        {
            _saving = false;
            IsEnabled = true;
            _saveFinished.TrySetResult();
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
        => DialogResult = false;

    private void OnCloseClick(object sender, RoutedEventArgs e)
        => Close();

    private void OnWindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_saving && e.Key == System.Windows.Input.Key.Escape)
        {
            DialogResult = false;
        }
    }
}
