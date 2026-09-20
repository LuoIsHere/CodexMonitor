using System.ComponentModel;
using System.Runtime.CompilerServices;
using LuoIsHere.CodexMonitor.Core.Localization;
using LuoIsHere.CodexMonitor.Infrastructure.Settings;
using LuoIsHere.CodexMonitor.Infrastructure.Startup;

namespace LuoIsHere.CodexMonitor.App.ViewModels;

public sealed class SettingsWindowViewModel : INotifyPropertyChanged
{
    private readonly AppSettings _sourceSettings;
    private string _language;
    private int _refreshIntervalMinutes;
    private bool _notificationsEnabled;
    private bool _showFiveHourQuota;
    private bool _showWeeklyQuota;
    private bool _showResetTimes;
    private bool _showSubscription;
    private bool _floatingWindowEnabled;
    private bool _floatingWindowLocked;
    private bool _floatingShowFiveHourQuota;
    private bool _floatingShowWeeklyQuota;
    private bool _floatingShowLastRefreshTime;
    private bool _floatingShowResetTimes;
    private bool _floatingShowSubscription;
    private bool _startupEnabled;
    private bool _minimizeToTray;
    private bool _registerCurrentPath;
    private string _startupStatus = "";
    private string _saveError = "";

    public SettingsWindowViewModel(AppSettings settings)
    {
        _sourceSettings = settings;
        _language = settings.Language;
        _startupEnabled = settings.Startup.Enabled;
        _minimizeToTray = settings.Startup.MinimizeToTray;
        _refreshIntervalMinutes = settings.RefreshIntervalMinutes;
        _notificationsEnabled = settings.Notifications.Enabled;
        _showFiveHourQuota = settings.Display.ShowFiveHourQuota;
        _showWeeklyQuota = settings.Display.ShowWeeklyQuota;
        _showResetTimes = settings.Display.ShowResetTimes;
        _showSubscription = settings.Display.ShowSubscription;
        _floatingWindowEnabled = settings.FloatingWindow.Enabled;
        _floatingWindowLocked = settings.FloatingWindow.IsLocked;
        _floatingShowFiveHourQuota = settings.FloatingWindow.Display.ShowFiveHourQuota;
        _floatingShowWeeklyQuota = settings.FloatingWindow.Display.ShowWeeklyQuota;
        _floatingShowLastRefreshTime = settings.FloatingWindow.Display.ShowLastRefreshTime;
        _floatingShowResetTimes = settings.FloatingWindow.Display.ShowResetTimes;
        _floatingShowSubscription = settings.FloatingWindow.Display.ShowSubscription;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<LanguageOption> Languages { get; } =
    [
        new("zh-CN", "简体中文"),
        new("en", "English"),
        new("zh-HK", "繁體中文（香港）"),
    ];

    public string Language
    {
        get => _language;
        set => SetField(ref _language, AppText.NormalizeLanguage(value));
    }

    public string RefreshIntervalText => AppText.Get("Minutes", RefreshIntervalMinutes);

    public bool StartupEnabled
    {
        get => _startupEnabled;
        set
        {
            SetField(ref _startupEnabled, value);
            if (!value)
            {
                RegisterCurrentPath = false;
            }
        }
    }

    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set => SetField(ref _minimizeToTray, value);
    }

    public bool RegisterCurrentPath
    {
        get => _registerCurrentPath;
        set => SetField(ref _registerCurrentPath, value);
    }

    public string StartupStatus
    {
        get => _startupStatus;
        set => SetField(ref _startupStatus, value);
    }

    public string SaveError
    {
        get => _saveError;
        set => SetField(ref _saveError, value);
    }

    public string WindowsControlNotice => UserStartupService.WindowsControlNotice;

    public int RefreshIntervalMinutes
    {
        get => _refreshIntervalMinutes;
        set
        {
            SetField(ref _refreshIntervalMinutes, Math.Clamp(value, 1, 60));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RefreshIntervalText)));
        }
    }

    public bool NotificationsEnabled
    {
        get => _notificationsEnabled;
        set => SetField(ref _notificationsEnabled, value);
    }

    public bool ShowFiveHourQuota
    {
        get => _showFiveHourQuota;
        set => SetField(ref _showFiveHourQuota, value);
    }

    public bool ShowWeeklyQuota
    {
        get => _showWeeklyQuota;
        set => SetField(ref _showWeeklyQuota, value);
    }

    public bool ShowResetTimes
    {
        get => _showResetTimes;
        set => SetField(ref _showResetTimes, value);
    }

    public bool ShowSubscription
    {
        get => _showSubscription;
        set => SetField(ref _showSubscription, value);
    }

    public bool FloatingWindowEnabled
    {
        get => _floatingWindowEnabled;
        set => SetField(ref _floatingWindowEnabled, value);
    }

    public bool FloatingWindowLocked
    {
        get => _floatingWindowLocked;
        set => SetField(ref _floatingWindowLocked, value);
    }

    public bool FloatingShowFiveHourQuota
    {
        get => _floatingShowFiveHourQuota;
        set => SetField(ref _floatingShowFiveHourQuota, value);
    }

    public bool FloatingShowWeeklyQuota
    {
        get => _floatingShowWeeklyQuota;
        set => SetField(ref _floatingShowWeeklyQuota, value);
    }

    public bool FloatingShowLastRefreshTime
    {
        get => _floatingShowLastRefreshTime;
        set => SetField(ref _floatingShowLastRefreshTime, value);
    }

    public bool FloatingShowResetTimes
    {
        get => _floatingShowResetTimes;
        set => SetField(ref _floatingShowResetTimes, value);
    }

    public bool FloatingShowSubscription
    {
        get => _floatingShowSubscription;
        set => SetField(ref _floatingShowSubscription, value);
    }

    public AppSettings CreateSettings()
        => _sourceSettings with
        {
            SchemaVersion = 4,
            Language = Language,
            Startup = new StartupSettings
            {
                Enabled = StartupEnabled,
                MinimizeToTray = MinimizeToTray,
            },
            RefreshIntervalMinutes = RefreshIntervalMinutes,
            Notifications = new NotificationSettings
            {
                Enabled = NotificationsEnabled,
            },
            Display = new DisplaySettings
            {
                ShowFiveHourQuota = ShowFiveHourQuota,
                ShowWeeklyQuota = ShowWeeklyQuota,
                ShowResetTimes = ShowResetTimes,
                ShowSubscription = ShowSubscription,
            },
            FloatingWindow = _sourceSettings.FloatingWindow with
            {
                Enabled = FloatingWindowEnabled,
                IsLocked = FloatingWindowLocked,
                Display = new FloatingWindowDisplaySettings
                {
                    ShowFiveHourQuota = FloatingShowFiveHourQuota,
                    ShowWeeklyQuota = FloatingShowWeeklyQuota,
                    ShowLastRefreshTime = FloatingShowLastRefreshTime,
                    ShowResetTimes = FloatingShowResetTimes,
                    ShowSubscription = FloatingShowSubscription,
                },
            },
        };

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record LanguageOption(string Code, string Name);
