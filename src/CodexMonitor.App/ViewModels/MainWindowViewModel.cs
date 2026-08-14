using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using LuoIsHere.CodexMonitor.Core.Formatting;
using LuoIsHere.CodexMonitor.Core.Models;
using LuoIsHere.CodexMonitor.Core.Refresh;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;

namespace LuoIsHere.CodexMonitor.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly QuotaRefreshService _refreshService;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly object _activeRefreshLock = new();
    private readonly HashSet<Task> _activeRefreshTasks = [];
    private readonly DispatcherTimer _refreshTimer;
    private DisplayPreferences _displayPreferences;
    private QuotaMonitorState _state;
    private DateTimeOffset? _lastNotifiedFailureAttemptAt;
    private bool _started;
    private bool _disposed;

    public MainWindowViewModel(
        QuotaRefreshService refreshService,
        TimeSpan refreshInterval,
        DisplayPreferences displayPreferences)
    {
        _refreshService = refreshService;
        _displayPreferences = displayPreferences;
        _state = refreshService.State;
        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = NormalizeRefreshInterval(refreshInterval),
        };
        _refreshTimer.Tick += OnRefreshTimerTick;
        RefreshCommand = new AsyncCommand(
            RefreshAsync,
            () => !_state.IsRefreshing);
        _refreshService.StateChanged += OnStateChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler<RefreshFailedEventArgs>? RefreshFailed;

    public AsyncCommand RefreshCommand { get; }

    public string AccountText => QuotaDisplayFormatter.FormatAccount(Snapshot?.Account);

    public string FiveHourPercent => QuotaDisplayFormatter.FormatPercent(Snapshot, Snapshot?.FiveHour);

    public string WeeklyPercent => QuotaDisplayFormatter.FormatPercent(Snapshot, Snapshot?.Weekly);

    public string FiveHourResetTime => QuotaDisplayFormatter.FormatResetTime(Snapshot, Snapshot?.FiveHour);

    public string WeeklyResetTime => QuotaDisplayFormatter.FormatResetTime(Snapshot, Snapshot?.Weekly);

    public string LastRefreshText => QuotaDisplayFormatter.FormatRefreshTime(Snapshot);

    public string StatusText => _state.IsRefreshing
        ? "正在刷新"
        : _state.Error is not null
            ? Snapshot is null ? "读取失败" : "数据可能已过期"
            : Snapshot is null ? "等待首次读取" : "正常";

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

    public async Task StartAsync()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        await RefreshAsync();
        if (!_disposed)
        {
            _refreshTimer.Start();
        }
    }

    public void ApplyPreferences(TimeSpan refreshInterval, DisplayPreferences displayPreferences)
    {
        _displayPreferences = displayPreferences;
        _refreshTimer.Interval = NormalizeRefreshInterval(refreshInterval);
        if (_started && !_disposed)
        {
            _refreshTimer.Stop();
            _refreshTimer.Start();
        }

        RaiseDisplaySettingsProperties();
    }

    public Task RefreshAsync()
    {
        Task refreshTask;
        lock (_activeRefreshLock)
        {
            if (_disposed)
            {
                return Task.CompletedTask;
            }

            refreshTask = _refreshService.RefreshAsync(_lifetime.Token);
            _activeRefreshTasks.Add(refreshTask);
        }

        return AwaitAndUntrackRefreshAsync(refreshTask);
    }

    private async Task AwaitAndUntrackRefreshAsync(Task refreshTask)
    {
        try
        {
            await refreshTask;
        }
        finally
        {
            lock (_activeRefreshLock)
            {
                _activeRefreshTasks.Remove(refreshTask);
            }
        }
    }

    private void OnStateChanged(object? sender, QuotaMonitorState state)
    {
        if (System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            ApplyState(state);
        }
        else
        {
            _ = System.Windows.Application.Current.Dispatcher.BeginInvoke(() => ApplyState(state));
        }
    }

    private void ApplyState(QuotaMonitorState state)
    {
        _state = state;
        RaiseDisplayProperties();
        RefreshCommand.NotifyCanExecuteChanged();

        if (state.Error is not null &&
            state.LastAttemptAt is DateTimeOffset attemptAt &&
            attemptAt != _lastNotifiedFailureAttemptAt)
        {
            _lastNotifiedFailureAttemptAt = attemptAt;
            RefreshFailed?.Invoke(this, new RefreshFailedEventArgs(state.Error, attemptAt));
        }
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

    private async void OnRefreshTimerTick(object? sender, EventArgs e)
    {
        _refreshTimer.Stop();
        try
        {
            await RefreshAsync();
        }
        finally
        {
            if (!_disposed)
            {
                _refreshTimer.Start();
            }
        }
    }

    private static TimeSpan NormalizeRefreshInterval(TimeSpan interval)
        => TimeSpan.FromMinutes(Math.Clamp(interval.TotalMinutes, 1, 60));

    private static Visibility ToVisibility(bool isVisible)
        => isVisible ? Visibility.Visible : Visibility.Collapsed;

    private static GridLength ToColumnWidth(bool isVisible)
        => isVisible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

    private static GridLength ToPixelWidth(bool isVisible)
        => new(isVisible ? 1 : 0);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public async ValueTask DisposeAsync()
    {
        Task[] activeRefreshTasks;
        lock (_activeRefreshLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            activeRefreshTasks = [.. _activeRefreshTasks];
        }

        _refreshService.StateChanged -= OnStateChanged;
        _refreshTimer.Stop();
        _refreshTimer.Tick -= OnRefreshTimerTick;
        await _lifetime.CancelAsync();

        try
        {
            await Task.WhenAll(activeRefreshTasks);
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }

        _refreshService.Dispose();
        _lifetime.Dispose();
    }
}

public sealed record DisplayPreferences(
    bool ShowFiveHourQuota,
    bool ShowWeeklyQuota,
    bool ShowResetTimes,
    bool ShowSubscription);

public sealed class RefreshFailedEventArgs(string message, DateTimeOffset attemptedAt) : EventArgs
{
    public string Message { get; } = message;

    public DateTimeOffset AttemptedAt { get; } = attemptedAt;
}
