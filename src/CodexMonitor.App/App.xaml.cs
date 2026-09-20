using System.IO;
using System.Windows;
using LuoIsHere.CodexMonitor.Core.Localization;
using LuoIsHere.CodexMonitor.App.ViewModels;
using LuoIsHere.CodexMonitor.Core.Abstractions;
using LuoIsHere.CodexMonitor.Core.Refresh;
using LuoIsHere.CodexMonitor.Infrastructure.Codex;
using LuoIsHere.CodexMonitor.Infrastructure.Logging;
using LuoIsHere.CodexMonitor.Infrastructure.Settings;
using LuoIsHere.CodexMonitor.Infrastructure.Startup;
using LuoIsHere.CodexMonitor.Infrastructure.Startup.Windows;

namespace LuoIsHere.CodexMonitor.App;

public partial class App : System.Windows.Application
{
    private MainWindow? _mainWindow;
    private SettingsWindow? _settingsWindow;
    private MainWindowViewModel? _viewModel;
    private TrayIconService? _trayIcon;
    private IFloatingWindowService? _floatingWindowService;
    private SingleInstanceCoordinator? _singleInstance;
    private JsonSettingsStore? _settingsStore;
    private IAppLogger? _logger;
    private readonly SemaphoreSlim _settingsSaveLock = new(1, 1);
    private AppSettings _settings = new();
    private bool _exitStarted;
    private bool _activationPending;
    private UserStartupService? _startupService;
    private StartupOptions _startupOptions = new(false);

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _startupOptions = StartupOptions.Parse(e.Args);

        try
        {
            _singleInstance = new SingleInstanceCoordinator();
            if (!_singleInstance.IsPrimaryInstance)
            {
                await _startupOptions.HandleSecondaryInstanceAsync(() => _singleInstance.RequestActivationAsync());
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
            ApplicationLocalizer.Apply(_settings.Language);
            _startupService = new UserStartupService(new WindowsRunRegistrationStore(), new StartupExecutableResolver());
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

            _floatingWindowService = new FloatingWindowService(
                _viewModel,
                _settings.FloatingWindow);
            _floatingWindowService.SettingsChanged += OnFloatingWindowSettingsChanged;

            _trayIcon = new TrayIconService(_floatingWindowService);
            _trayIcon.OpenRequested += OnTrayOpenRequested;
            _trayIcon.MinimizeRequested += OnTrayMinimizeRequested;
            _trayIcon.SettingsRequested += OnTraySettingsRequested;
            _trayIcon.ExitRequested += OnTrayExitRequested;

            if (_startupOptions.ShouldShowMainWindow(_settings.Startup))
            {
                _mainWindow.Show();
            }
            UpdateTrayWindowState();
            if (_activationPending)
            {
                _activationPending = false;
                ActivateCurrentWindow();
            }

            await _viewModel.StartAsync();
        }
        catch (Exception exception)
        {
            _logger?.Error("Application startup failed.", exception);
            if (!_startupOptions.IsAutomatic)
            {
                System.Windows.MessageBox.Show(
                    AppText.Get("StartupFailed", exception.Message),
                    "CodexMonitor",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            await DisposeRuntimeAsync();
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

    private void OnTraySettingsRequested(object? sender, EventArgs e)
        => ShowSettings();

    private async void OnTrayExitRequested(object? sender, EventArgs e)
        => await ExitApplicationAsync();

    private void OnRefreshFailed(object? sender, RefreshFailedEventArgs e)
    {
        if (_settings.Notifications.Enabled)
        {
            _trayIcon?.ShowRefreshFailureNotification(e.Message);
        }
    }

    private async void OnFloatingWindowSettingsChanged(
        object? sender,
        FloatingWindowSettingsChangedEventArgs e)
    {
        _settings = _settings with { FloatingWindow = e.Settings };
        await SaveCurrentSettingsWithoutDialogAsync();
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
        if (_exitStarted)
        {
            return;
        }

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

    private void ShowSettings()
    {
        if (_exitStarted)
        {
            return;
        }

        if (_settingsWindow is not null)
        {
            ActivateCurrentWindow();
            return;
        }

        if (_startupService is null)
        {
            return;
        }

        var settingsWindow = new SettingsWindow(_settings, _startupService, SaveEditedSettingsAsync);
        if (_mainWindow?.IsVisible == true)
        {
            settingsWindow.Owner = _mainWindow;
        }
        else
        {
            settingsWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        _settingsWindow = settingsWindow;
        settingsWindow.ShowDialog();
        _settingsWindow = null;
    }

    private async Task SaveEditedSettingsAsync(AppSettings settings)
    {
        if (_settingsStore is null)
        {
            throw new InvalidOperationException(AppText.Get("SettingsNotReady"));
        }

        await _settingsSaveLock.WaitAsync();
        try
        {
            // A floating window can move while the settings dialog is open.
            var saved = settings with
            {
                FloatingWindow = settings.FloatingWindow with
                {
                    Left = _settings.FloatingWindow.Left,
                    Top = _settings.FloatingWindow.Top,
                },
            };
            await _settingsStore.SaveAsync(saved);
            _settings = saved;
            ApplicationLocalizer.Apply(saved.Language);
            _viewModel?.ApplyPreferences(
                TimeSpan.FromMinutes(saved.RefreshIntervalMinutes),
                CreateDisplayPreferences(saved.Display));
            _floatingWindowService?.ApplySettings(saved.FloatingWindow);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger?.Error("Settings could not be saved.", exception);
            throw;
        }
        finally
        {
            _settingsSaveLock.Release();
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
            await _settingsWindow.WaitForSaveAsync();
            _settingsWindow?.Close();
            _settingsWindow = null;
        }

        try
        {
            await SaveCurrentSettingsAsync();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger?.Error("Settings could not be saved during shutdown.", exception);
        }

        await DisposeRuntimeAsync();
        Shutdown();
    }

    private async Task DisposeRuntimeAsync()
    {
        DisposeTrayIcon();

        if (_floatingWindowService is not null)
        {
            _floatingWindowService.SettingsChanged -= OnFloatingWindowSettingsChanged;
            _floatingWindowService.Dispose();
            _floatingWindowService = null;
        }

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

    private async Task SaveCurrentSettingsWithoutDialogAsync()
    {
        if (_settingsStore is null || _exitStarted)
        {
            return;
        }

        try
        {
            await SaveCurrentSettingsAsync();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger?.Error("Floating-window settings could not be saved.", exception);
        }
    }

    private async Task SaveCurrentSettingsAsync()
    {
        if (_settingsStore is null)
        {
            return;
        }

        await _settingsSaveLock.WaitAsync();
        try
        {
            await _settingsStore.SaveAsync(_settings);
        }
        finally
        {
            _settingsSaveLock.Release();
        }
    }

    private static DisplayPreferences CreateDisplayPreferences(DisplaySettings settings)
        => new(
            settings.ShowFiveHourQuota,
            settings.ShowWeeklyQuota,
            settings.ShowResetTimes,
            settings.ShowSubscription);
}
