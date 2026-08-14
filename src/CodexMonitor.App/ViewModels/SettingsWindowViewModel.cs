using System.ComponentModel;
using System.Runtime.CompilerServices;
using LuoIsHere.CodexMonitor.Infrastructure.Settings;

namespace LuoIsHere.CodexMonitor.App.ViewModels;

public sealed class SettingsWindowViewModel : INotifyPropertyChanged
{
    private readonly AppSettings _sourceSettings;
    private int _refreshIntervalMinutes;
    private bool _notificationsEnabled;
    private bool _showFiveHourQuota;
    private bool _showWeeklyQuota;
    private bool _showResetTimes;
    private bool _showSubscription;

    public SettingsWindowViewModel(AppSettings settings)
    {
        _sourceSettings = settings;
        _refreshIntervalMinutes = settings.RefreshIntervalMinutes;
        _notificationsEnabled = settings.Notifications.Enabled;
        _showFiveHourQuota = settings.Display.ShowFiveHourQuota;
        _showWeeklyQuota = settings.Display.ShowWeeklyQuota;
        _showResetTimes = settings.Display.ShowResetTimes;
        _showSubscription = settings.Display.ShowSubscription;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int RefreshIntervalMinutes
    {
        get => _refreshIntervalMinutes;
        set => SetField(ref _refreshIntervalMinutes, Math.Clamp(value, 1, 60));
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

    public AppSettings CreateSettings()
        => _sourceSettings with
        {
            SchemaVersion = 2,
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
