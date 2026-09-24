using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using LuoIsHere.CodexMonitor.App.Monitoring;
using LuoIsHere.CodexMonitor.Core.Formatting;
using LuoIsHere.CodexMonitor.Core.Localization;
using LuoIsHere.CodexMonitor.Core.Models;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;

namespace LuoIsHere.CodexMonitor.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly QuotaMonitorCoordinator _monitor;
    private DisplayPreferences _displayPreferences;
    private QuotaMonitorState _state;
    private bool _disposed;

    public MainWindowViewModel(
        QuotaMonitorCoordinator monitor,
        DisplayPreferences displayPreferences)
    {
        _monitor = monitor;
        _displayPreferences = displayPreferences;
        _state = monitor.State;
        RefreshCommand = new AsyncCommand(
            monitor.RefreshAsync,
            () => !_disposed && !_state.IsRefreshing);
        _monitor.StateChanged += OnStateChanged;
        AppText.LanguageChanged += OnLanguageChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AsyncCommand RefreshCommand { get; }

    public string AccountText => QuotaDisplayFormatter.FormatAccount(Snapshot?.Account);

    public string FiveHourPercent => QuotaDisplayFormatter.FormatPercent(Snapshot, Snapshot?.FiveHour);

    public string WeeklyPercent => QuotaDisplayFormatter.FormatPercent(Snapshot, Snapshot?.Weekly);

    public string FiveHourResetTime => QuotaDisplayFormatter.FormatResetTime(Snapshot, Snapshot?.FiveHour);

    public string WeeklyResetTime => QuotaDisplayFormatter.FormatResetTime(Snapshot, Snapshot?.Weekly);

    public string LastRefreshText => QuotaDisplayFormatter.FormatRefreshTime(Snapshot);

    public string StatusText => _state.IsRefreshing
        ? AppText.Get("Refreshing")
        : _state.Error is not null
            ? Snapshot is null ? AppText.Get("ReadFailed") : AppText.Get("Stale")
            : Snapshot is null ? AppText.Get("Waiting") : AppText.Get("Healthy");

    public MediaBrush StatusBrush => _state.IsRefreshing
        ? MediaBrushes.DodgerBlue
        : _state.Error is not null
            ? Snapshot is null ? MediaBrushes.Firebrick : MediaBrushes.DarkOrange
            : Snapshot is null ? MediaBrushes.Gray : MediaBrushes.ForestGreen;

    public Visibility FiveHourVisibility => ToVisibility(_displayPreferences.ShowFiveHourQuota);

    public Visibility WeeklyVisibility => ToVisibility(_displayPreferences.ShowWeeklyQuota);

    public Visibility ResetTimesVisibility => ToVisibility(_displayPreferences.ShowResetTimes);

    public Visibility SubscriptionVisibility => ToVisibility(_displayPreferences.ShowSubscription);

    public GridLength FiveHourColumnWidth => ToColumnWidth(_displayPreferences.ShowFiveHourQuota);

    public GridLength WeeklyColumnWidth => ToColumnWidth(_displayPreferences.ShowWeeklyQuota);

    public GridLength ResetTimesColumnWidth => ToColumnWidth(_displayPreferences.ShowResetTimes);

    public GridLength FirstDividerWidth => ToPixelWidth(
        _displayPreferences.ShowFiveHourQuota &&
        (_displayPreferences.ShowWeeklyQuota || _displayPreferences.ShowResetTimes));

    public GridLength SecondDividerWidth => ToPixelWidth(
        _displayPreferences.ShowWeeklyQuota && _displayPreferences.ShowResetTimes);

    private QuotaSnapshot? Snapshot => _state.LastSuccessfulSnapshot;

    public void ApplyDisplayPreferences(DisplayPreferences displayPreferences)
    {
        _displayPreferences = displayPreferences;
        RaiseDisplaySettingsProperties();
    }

    private void OnStateChanged(object? sender, QuotaMonitorState state)
    {
        if (_disposed)
        {
            return;
        }

        _state = state;
        RaiseDisplayProperties();
        RefreshCommand.NotifyCanExecuteChanged();
    }

    private void RaiseDisplayProperties()
    {
        OnPropertyChanged(nameof(AccountText));
        OnPropertyChanged(nameof(FiveHourPercent));
        OnPropertyChanged(nameof(WeeklyPercent));
        OnPropertyChanged(nameof(FiveHourResetTime));
        OnPropertyChanged(nameof(WeeklyResetTime));
        OnPropertyChanged(nameof(LastRefreshText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusBrush));
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => RaiseDisplayProperties();

    private void RaiseDisplaySettingsProperties()
    {
        OnPropertyChanged(nameof(FiveHourVisibility));
        OnPropertyChanged(nameof(WeeklyVisibility));
        OnPropertyChanged(nameof(ResetTimesVisibility));
        OnPropertyChanged(nameof(SubscriptionVisibility));
        OnPropertyChanged(nameof(FiveHourColumnWidth));
        OnPropertyChanged(nameof(WeeklyColumnWidth));
        OnPropertyChanged(nameof(ResetTimesColumnWidth));
        OnPropertyChanged(nameof(FirstDividerWidth));
        OnPropertyChanged(nameof(SecondDividerWidth));
    }

    private static Visibility ToVisibility(bool isVisible)
        => isVisible ? Visibility.Visible : Visibility.Collapsed;

    private static GridLength ToColumnWidth(bool isVisible)
        => isVisible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

    private static GridLength ToPixelWidth(bool isVisible)
        => new(isVisible ? 1 : 0);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _monitor.StateChanged -= OnStateChanged;
        AppText.LanguageChanged -= OnLanguageChanged;
        RefreshCommand.NotifyCanExecuteChanged();
    }
}

public sealed record DisplayPreferences(
    bool ShowFiveHourQuota,
    bool ShowWeeklyQuota,
    bool ShowResetTimes,
    bool ShowSubscription);

