using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using LuoIsHere.CodexMonitor.App.Monitoring;
using LuoIsHere.CodexMonitor.Core.Formatting;
using LuoIsHere.CodexMonitor.Core.Localization;
using LuoIsHere.CodexMonitor.Core.Models;
using LuoIsHere.CodexMonitor.Infrastructure.Settings;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;

namespace LuoIsHere.CodexMonitor.App.ViewModels;

public sealed class FloatingWindowViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly QuotaMonitorCoordinator _monitor;
    private QuotaMonitorState _state;
    private FloatingWindowDisplaySettings _display;
    private bool _disposed;

    public FloatingWindowViewModel(
        QuotaMonitorCoordinator monitor,
        FloatingWindowDisplaySettings display)
    {
        _monitor = monitor;
        _state = monitor.State;
        _display = display;
        _monitor.StateChanged += OnStateChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string FiveHourPercent => QuotaDisplayFormatter.FormatPercent(Snapshot, Snapshot?.FiveHour);

    public string WeeklyPercent => QuotaDisplayFormatter.FormatPercent(Snapshot, Snapshot?.Weekly);

    public string FiveHourResetTime => QuotaDisplayFormatter.FormatResetTime(Snapshot, Snapshot?.FiveHour);

    public string WeeklyResetTime => QuotaDisplayFormatter.FormatResetTime(Snapshot, Snapshot?.Weekly);

    public string AccountText => QuotaDisplayFormatter.FormatAccount(Snapshot?.Account, "en");

    public string LastRefreshText => QuotaDisplayFormatter.FormatRefreshTime(Snapshot);

    public MediaBrush StatusBrush => _state.IsRefreshing
        ? MediaBrushes.DodgerBlue
        : _state.Error is not null
            ? Snapshot is null ? MediaBrushes.Firebrick : MediaBrushes.DarkOrange
            : Snapshot is null ? MediaBrushes.Gray : MediaBrushes.ForestGreen;

    public string StatusText => _state.IsRefreshing
        ? AppText.GetForLanguage("Refreshing", "en")
        : _state.Error is not null
            ? Snapshot is null
                ? AppText.GetForLanguage("ReadFailed", "en")
                : AppText.GetForLanguage("Stale", "en")
            : Snapshot is null
                ? AppText.GetForLanguage("Waiting", "en")
                : AppText.GetForLanguage("Healthy", "en");

    private QuotaSnapshot? Snapshot => _state.LastSuccessfulSnapshot;

    public Visibility TopSummaryVisibility => ToVisibility(
        _display.ShowFiveHourQuota ||
        _display.ShowWeeklyQuota ||
        _display.ShowLastRefreshTime);

    public Visibility FiveHourVisibility => ToVisibility(_display.ShowFiveHourQuota);

    public Visibility WeeklyVisibility => ToVisibility(_display.ShowWeeklyQuota);

    public Visibility LastRefreshVisibility => ToVisibility(_display.ShowLastRefreshTime);

    public Visibility ResetTimesVisibility => ToVisibility(_display.ShowResetTimes);

    public Visibility SubscriptionVisibility => ToVisibility(_display.ShowSubscription);

    public Visibility FirstDividerVisibility => ToVisibility(
        _display.ShowFiveHourQuota &&
        (_display.ShowWeeklyQuota || _display.ShowLastRefreshTime));

    public Visibility SecondDividerVisibility => ToVisibility(
        _display.ShowWeeklyQuota && _display.ShowLastRefreshTime);

    public void ApplyDisplaySettings(FloatingWindowDisplaySettings display)
    {
        _display = display;
        OnPropertyChanged(nameof(TopSummaryVisibility));
        OnPropertyChanged(nameof(FiveHourVisibility));
        OnPropertyChanged(nameof(WeeklyVisibility));
        OnPropertyChanged(nameof(LastRefreshVisibility));
        OnPropertyChanged(nameof(ResetTimesVisibility));
        OnPropertyChanged(nameof(SubscriptionVisibility));
        OnPropertyChanged(nameof(FirstDividerVisibility));
        OnPropertyChanged(nameof(SecondDividerVisibility));
    }

    private void OnStateChanged(object? sender, QuotaMonitorState state)
    {
        if (_disposed)
        {
            return;
        }

        _state = state;
        OnPropertyChanged(nameof(FiveHourPercent));
        OnPropertyChanged(nameof(WeeklyPercent));
        OnPropertyChanged(nameof(FiveHourResetTime));
        OnPropertyChanged(nameof(WeeklyResetTime));
        OnPropertyChanged(nameof(AccountText));
        OnPropertyChanged(nameof(LastRefreshText));
        OnPropertyChanged(nameof(StatusBrush));
        OnPropertyChanged(nameof(StatusText));
    }

    private static Visibility ToVisibility(bool value)
        => value ? Visibility.Visible : Visibility.Collapsed;

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
    }
}
