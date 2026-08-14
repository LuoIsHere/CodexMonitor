using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using LuoIsHere.CodexMonitor.Core.Formatting;
using LuoIsHere.CodexMonitor.Core.Models;
using LuoIsHere.CodexMonitor.Core.Refresh;

namespace LuoIsHere.CodexMonitor.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly QuotaRefreshService _refreshService;
    private readonly TimeSpan _refreshInterval;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly DispatcherTimer _clockTimer;
    private Task? _periodicTask;
    private QuotaMonitorState _state;
    private bool _started;

    public MainWindowViewModel(QuotaRefreshService refreshService, TimeSpan refreshInterval)
    {
        _refreshService = refreshService;
        _refreshInterval = refreshInterval;
        _state = refreshService.State;
        RefreshCommand = new AsyncCommand(
            () => _refreshService.RefreshAsync(_lifetime.Token),
            () => !_state.IsRefreshing);
        _clockTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _clockTimer.Tick += OnClockTick;
        _refreshService.StateChanged += OnStateChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AsyncCommand RefreshCommand { get; }

    public string FiveHourPercent => QuotaDisplayFormatter.FormatPercent(Snapshot?.FiveHour);

    public string FiveHourCountdown => QuotaDisplayFormatter.FormatCountdown(
        Snapshot?.FiveHour,
        DateTimeOffset.Now,
        includeSeconds: true);

    public string WeeklyPercent => QuotaDisplayFormatter.FormatPercent(Snapshot?.Weekly);

    public string WeeklyCountdown => QuotaDisplayFormatter.FormatCountdown(
        Snapshot?.Weekly,
        DateTimeOffset.Now,
        includeSeconds: false);

    public string LastRefreshText => QuotaDisplayFormatter.FormatRefreshTime(Snapshot);

    public string StatusText => _state.IsRefreshing
        ? "正在刷新"
        : _state.Error is not null
            ? Snapshot is null ? "读取失败" : "数据可能已过期"
            : Snapshot is null ? "等待首次读取" : "正常";

    public Brush StatusBrush => _state.IsRefreshing
        ? Brushes.DodgerBlue
        : _state.Error is not null
            ? Snapshot is null ? Brushes.Firebrick : Brushes.DarkOrange
            : Snapshot is null ? Brushes.Gray : Brushes.ForestGreen;

    public string ErrorText => _state.Error ?? string.Empty;

    public string RefreshIntervalText => $"每 {(int)_refreshInterval.TotalMinutes} 分钟自动刷新";

    private QuotaSnapshot? Snapshot => _state.LastSuccessfulSnapshot;

    public async Task StartAsync()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _clockTimer.Start();
        await _refreshService.RefreshAsync(_lifetime.Token);
        _periodicTask = _refreshService.RunPeriodicAsync(_refreshInterval, _lifetime.Token);
    }

    private void OnStateChanged(object? sender, QuotaMonitorState state)
    {
        if (Application.Current.Dispatcher.CheckAccess())
        {
            ApplyState(state);
        }
        else
        {
            _ = Application.Current.Dispatcher.BeginInvoke(() => ApplyState(state));
        }
    }

    private void ApplyState(QuotaMonitorState state)
    {
        _state = state;
        RaiseDisplayProperties();
        RefreshCommand.NotifyCanExecuteChanged();
    }

    private void OnClockTick(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(FiveHourCountdown));
        OnPropertyChanged(nameof(WeeklyCountdown));
        OnPropertyChanged(nameof(LastRefreshText));
    }

    private void RaiseDisplayProperties()
    {
        OnPropertyChanged(nameof(FiveHourPercent));
        OnPropertyChanged(nameof(FiveHourCountdown));
        OnPropertyChanged(nameof(WeeklyPercent));
        OnPropertyChanged(nameof(WeeklyCountdown));
        OnPropertyChanged(nameof(LastRefreshText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusBrush));
        OnPropertyChanged(nameof(ErrorText));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public async ValueTask DisposeAsync()
    {
        _clockTimer.Stop();
        _clockTimer.Tick -= OnClockTick;
        _refreshService.StateChanged -= OnStateChanged;
        await _lifetime.CancelAsync();

        if (_periodicTask is not null)
        {
            try
            {
                await _periodicTask;
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown.
            }
        }

        _refreshService.Dispose();
        _lifetime.Dispose();
    }
}

