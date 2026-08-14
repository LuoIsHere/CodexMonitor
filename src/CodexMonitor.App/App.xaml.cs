using System.Windows;
using LuoIsHere.CodexMonitor.App.ViewModels;
using LuoIsHere.CodexMonitor.Core.Refresh;
using LuoIsHere.CodexMonitor.Infrastructure.Codex;
using LuoIsHere.CodexMonitor.Infrastructure.Logging;
using LuoIsHere.CodexMonitor.Infrastructure.Settings;

namespace LuoIsHere.CodexMonitor.App;

public partial class App : System.Windows.Application
{
    private MainWindow? _mainWindow;
    private MainWindowViewModel? _viewModel;
    private TrayIconService? _trayIcon;
    private bool _exitStarted;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var logger = new FileAppLogger();
            var settings = await new JsonSettingsStore(logger).LoadAsync();
            var provider = new CodexAppServerQuotaProvider(
                new CodexExecutableLocator(),
                settings.CodexExecutable,
                logger);
            var refreshService = new QuotaRefreshService(provider, logger);
            _viewModel = new MainWindowViewModel(
                refreshService,
                TimeSpan.FromMinutes(settings.RefreshIntervalMinutes));

            _mainWindow = new MainWindow(_viewModel);
            _mainWindow.HiddenToTray += OnWindowHiddenToTray;
            MainWindow = _mainWindow;

            _trayIcon = new TrayIconService();
            _trayIcon.OpenRequested += OnTrayOpenRequested;
            _trayIcon.RefreshRequested += OnTrayRefreshRequested;
            _trayIcon.ExitRequested += OnTrayExitRequested;

            _mainWindow.Show();
        }
        catch (Exception exception)
        {
            DisposeTrayIcon();
            System.Windows.MessageBox.Show(
                $"CodexMonitor 启动失败。\n\n{exception.Message}",
                "CodexMonitor",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void OnWindowHiddenToTray(object? sender, EventArgs e)
        => _trayIcon?.ShowBackgroundNotificationOnce();

    private void OnTrayOpenRequested(object? sender, EventArgs e)
        => _mainWindow?.ShowFromTray();

    private async void OnTrayRefreshRequested(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            await _viewModel.RefreshAsync();
        }
    }

    private async void OnTrayExitRequested(object? sender, EventArgs e)
        => await ExitApplicationAsync();

    private async Task ExitApplicationAsync()
    {
        if (_exitStarted)
        {
            return;
        }

        _exitStarted = true;
        DisposeTrayIcon();

        if (_viewModel is not null)
        {
            await _viewModel.DisposeAsync();
            _viewModel = null;
        }

        if (_mainWindow is not null)
        {
            _mainWindow.HiddenToTray -= OnWindowHiddenToTray;
            _mainWindow.CloseForExit();
            _mainWindow = null;
        }

        Shutdown();
    }

    private void DisposeTrayIcon()
    {
        if (_trayIcon is null)
        {
            return;
        }

        _trayIcon.OpenRequested -= OnTrayOpenRequested;
        _trayIcon.RefreshRequested -= OnTrayRefreshRequested;
        _trayIcon.ExitRequested -= OnTrayExitRequested;
        _trayIcon.Dispose();
        _trayIcon = null;
    }
}
