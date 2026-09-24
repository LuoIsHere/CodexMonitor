using System.Windows.Threading;
using LuoIsHere.CodexMonitor.Core.Models;
using LuoIsHere.CodexMonitor.Core.Refresh;

namespace LuoIsHere.CodexMonitor.App.Monitoring;

// Application-owned monitor lifecycle, shared by both windows.
public sealed class QuotaMonitorCoordinator : IAsyncDisposable
{
    private readonly QuotaRefreshService _refreshService;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly object _activeRefreshLock = new();
    private readonly HashSet<Task> _activeRefreshTasks = [];
    private readonly DispatcherTimer _refreshTimer;
    private DateTimeOffset? _lastNotifiedFailureAttemptAt;
    private bool _started;
    private bool _disposed;

    public QuotaMonitorCoordinator(QuotaRefreshService refreshService, TimeSpan refreshInterval)
    {
        _refreshService = refreshService;
        State = refreshService.State;
        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = NormalizeRefreshInterval(refreshInterval),
        };
        _refreshTimer.Tick += OnRefreshTimerTick;
        _refreshService.StateChanged += OnStateChanged;
    }

    public event EventHandler<QuotaMonitorState>? StateChanged;

    public event EventHandler<RefreshFailedEventArgs>? RefreshFailed;

    public QuotaMonitorState State { get; private set; }

    public async Task StartAsync()
    {
        if (_started || _disposed)
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

    public void SetRefreshInterval(TimeSpan refreshInterval)
    {
        if (_disposed)
        {
            return;
        }

        _refreshTimer.Interval = NormalizeRefreshInterval(refreshInterval);
        if (_started)
        {
            _refreshTimer.Stop();
            _refreshTimer.Start();
        }
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
        if (_disposed)
        {
            return;
        }

        State = state;
        StateChanged?.Invoke(this, state);
        if (state.Error is not null &&
            state.LastAttemptAt is DateTimeOffset attemptAt &&
            attemptAt != _lastNotifiedFailureAttemptAt)
        {
            _lastNotifiedFailureAttemptAt = attemptAt;
            RefreshFailed?.Invoke(this, new RefreshFailedEventArgs(state.Error, attemptAt));
        }
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

public sealed class RefreshFailedEventArgs(string message, DateTimeOffset attemptedAt) : EventArgs
{
    public string Message { get; } = message;

    public DateTimeOffset AttemptedAt { get; } = attemptedAt;
}
