using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Button = System.Windows.Controls.Button;
using ToolTip = System.Windows.Controls.ToolTip;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
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
    private ToolTip? _openHelp;

    public SettingsWindow(AppSettings settings, UserStartupService startup, Func<AppSettings, Task> saveSettings)
    {
        InitializeComponent();
        _viewModel = new SettingsWindowViewModel(settings);
        _startup = startup;
        _session = new StartupSettingsSession(settings, startup, saveSettings);
        _viewModel.StartupStatus = startup.ReadStatus();
        DataContext = _viewModel;
        Closing += (_, e) => e.Cancel = _saving;
        Deactivated += (_, _) => CloseHelp();
        Closed += (_, _) => CloseHelp();
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

    private void OnHelpClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ToolTip: ToolTip tip } button)
        {
            var wasOpen = tip.IsOpen;
            CloseHelp();
            if (!wasOpen)
            {
                tip.PlacementTarget = button;
                tip.Placement = PlacementMode.Bottom;
                _openHelp = tip;
                tip.IsOpen = true;
            }
            else
            {
                tip.IsOpen = false;
            }
        }
        e.Handled = true;
    }

    private void OnHelpPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            if (!e.IsRepeat) OnHelpClick(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && sender is Button { ToolTip: ToolTip { IsOpen: true } tip })
        {
            tip.IsOpen = false;
            CloseHelp();
            e.Handled = true;
        }
    }

    private void OnHelpLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        => CloseHelp();

    private void OnHelpUnloaded(object sender, RoutedEventArgs e)
        => CloseHelp();

    private void CloseHelp()
    {
        if (_openHelp is not null) _openHelp.IsOpen = false;
        _openHelp = null;
    }

    private void OnWindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_saving && e.Key == System.Windows.Input.Key.Escape)
        {
            DialogResult = false;
        }
    }
}
