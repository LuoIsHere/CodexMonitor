using System.IO;
using System.Windows;
using LuoIsHere.CodexMonitor.App.ViewModels;
using LuoIsHere.CodexMonitor.Core.Abstractions;
using LuoIsHere.CodexMonitor.Core.Refresh;
using LuoIsHere.CodexMonitor.Infrastructure.Codex;
using LuoIsHere.CodexMonitor.Infrastructure.Logging;
using LuoIsHere.CodexMonitor.Infrastructure.Settings;

namespace LuoIsHere.CodexMonitor.App;

public partial class App : System.Windows.Application
{
    private MainWindow? _mainWindow;
    private SettingsWindow? _settingsWindow;
    private MainWindowViewModel? _viewModel;
    private TrayIconService? _trayIcon;
    private SingleInstanceCoordinator? _singleInstance;
    private JsonSettingsStore? _settingsStore;
    private IAppLogger? _logger;
    private AppSettings _settings = new();
    private bool _exitStarted;
    private bool _activationPending;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            _singleInstance = new SingleInstanceCoordinator();
            if (!_singleInstance.IsPrimaryInstance)
            {
                _ = await _singleInstance.RequestActivationAsync();
                await _singleInstance.DisposeAsync();
                _singleInstance = null;
                Shutdown();
                return;
            }

            _singleInstance.ActivationRequested += OnActivationRequested;
            _singleInstance.StartListening();

            _logger = new FileAppLogger();
            _settingsStore = new JsonSettingsStore(_logger);
            _settings = await _settingsStore.LoadAsync();
            var provider = new CodexAppServerQuotaProvider(
                new CodexExecutableLocator(),
                _settings.CodexExecutable,
                _logger);
            var refreshService = new QuotaRefreshService(provider, _logger);
            _viewModel = new MainWindowViewModel(
                refreshService,
                TimeSpan.FromMinutes(_settings.RefreshIntervalMinutes),
                CreateDisplayPreferences(_settings.Display));
            _viewModel.RefreshFailed += OnRefreshFailed;

            _mainWindow = new MainWindow(_viewModel);
            _mainWindow.HiddenToTray += OnWindowHiddenToTray;
            _mainWindow.StateChanged += OnMainWindowStateChanged;
            _mainWindow.IsVisibleChanged += OnMainWindowIsVisibleChanged;
            MainWindow = _mainWindow;

            _trayIcon = new TrayIconService(new UnavailableFloatingWindowService());
            _trayIcon.OpenRequested += OnTrayOpenRequested;
            _trayIcon.MinimizeRequested += OnTrayMinimizeRequested;
            _trayIcon.SettingsRequested += OnTraySettingsRequested;
            _trayIcon.ExitRequested += OnTrayExitRequested;

            _mainWindow.Show();
            UpdateTrayWindowState();
            if (_activationPending)
            {
                _activationPending = false;
                ActivateCurrentWindow();
            }
        }
        catch (Exception exception)
        {
            DisposeTrayIcon();
            System.Windows.MessageBox.Show(
                $"CodexMonitor 启动失败。\n\n{exception.Message}",
                "CodexMonitor",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            await DisposeSingleInstanceAsync();
            Shutdown(1);
        }
    }

    private void OnWindowHiddenToTray(object? sender, EventArgs e)
    {
        UpdateTrayWindowState();
        if (_settings.Notifications.Enabled)
        {
            _trayIcon?.ShowBackgroundNotificationOnce();
        }
    }

    private void OnTrayOpenRequested(object? sender, EventArgs e)
        => ActivateCurrentWindow();

    private void OnTrayMinimizeRequested(object? sender, EventArgs e)
        => _mainWindow?.MinimizeFromTray();

    private async void OnTraySettingsRequested(object? sender, EventArgs e)
        => await ShowSettingsAsync();

    private async void OnTrayExitRequested(object? sender, EventArgs e)
        => await ExitApplicationAsync();

    private void OnRefreshFailed(object? sender, RefreshFailedEventArgs e)
    {
        if (_settings.Notifications.Enabled)
        {
            _trayIcon?.ShowRefreshFailureNotification(e.Message);
        }
    }

    private void OnActivationRequested(object? sender, EventArgs e)
    {
        _ = Dispatcher.BeginInvoke(() =>
        {
            if (_mainWindow is null && _settingsWindow is null)
            {
                _activationPending = true;
                return;
            }

            ActivateCurrentWindow();
        });
    }

    private void ActivateCurrentWindow()
    {
        if (_settingsWindow is not null)
        {
            if (_settingsWindow.WindowState == WindowState.Minimized)
            {
                _settingsWindow.WindowState = WindowState.Normal;
            }

            _settingsWindow.Show();
            _settingsWindow.Activate();
            return;
        }

        _mainWindow?.ShowFromTray();
    }

    private async Task ShowSettingsAsync()
    {
        if (_settingsWindow is not null)
        {
            ActivateCurrentWindow();
            return;
        }

        var settingsWindow = new SettingsWindow(_settings);
        if (_mainWindow?.IsVisible == true)
        {
            settingsWindow.Owner = _mainWindow;
        }
        else
        {
            settingsWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        _settingsWindow = settingsWindow;
        var saved = settingsWindow.ShowDialog() == true
            ? settingsWindow.SavedSettings
            : null;
        _settingsWindow = null;

        if (saved is null || _settingsStore is null)
        {
            return;
        }

        try
        {
            await _settingsStore.SaveAsync(saved);
            _settings = saved;
            _viewModel?.ApplyPreferences(
                TimeSpan.FromMinutes(saved.RefreshIntervalMinutes),
                CreateDisplayPreferences(saved.Display));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger?.Error("Settings could not be saved.", exception);
            System.Windows.MessageBox.Show(
                $"设置保存失败。\n\n{exception.Message}",
                "CodexMonitor",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnMainWindowStateChanged(object? sender, EventArgs e)
        => UpdateTrayWindowState();

    private void OnMainWindowIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        => UpdateTrayWindowState();

    private void UpdateTrayWindowState()
        => _trayIcon?.SetCanMinimize(
            _mainWindow?.IsVisible == true && _mainWindow.WindowState != WindowState.Minimized);

    private async Task ExitApplicationAsync()
    {
        if (_exitStarted)
        {
            return;
        }

        _exitStarted = true;
        if (_settingsWindow is not null)
        {
            _settingsWindow.Close();
            _settingsWindow = null;
        }

        DisposeTrayIcon();

        if (_viewModel is not null)
        {
            _viewModel.RefreshFailed -= OnRefreshFailed;
            await _viewModel.DisposeAsync();
            _viewModel = null;
        }

        if (_mainWindow is not null)
        {
            _mainWindow.HiddenToTray -= OnWindowHiddenToTray;
            _mainWindow.StateChanged -= OnMainWindowStateChanged;
            _mainWindow.IsVisibleChanged -= OnMainWindowIsVisibleChanged;
            _mainWindow.CloseForExit();
            _mainWindow = null;
        }

        await DisposeSingleInstanceAsync();
        Shutdown();
    }

    private void DisposeTrayIcon()
    {
        if (_trayIcon is null)
        {
            return;
        }

        _trayIcon.OpenRequested -= OnTrayOpenRequested;
        _trayIcon.MinimizeRequested -= OnTrayMinimizeRequested;
        _trayIcon.SettingsRequested -= OnTraySettingsRequested;
        _trayIcon.ExitRequested -= OnTrayExitRequested;
        _trayIcon.Dispose();
        _trayIcon = null;
    }

    private async Task DisposeSingleInstanceAsync()
    {
        if (_singleInstance is null)
        {
            return;
        }

        _singleInstance.ActivationRequested -= OnActivationRequested;
        await _singleInstance.DisposeAsync();
        _singleInstance = null;
    }

    private static DisplayPreferences CreateDisplayPreferences(DisplaySettings settings)
        => new(
            settings.ShowFiveHourQuota,
            settings.ShowWeeklyQuota,
            settings.ShowResetTimes,
            settings.ShowSubscription);
}
