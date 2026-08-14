using LuoIsHere.CodexMonitor.Core.Abstractions;
using LuoIsHere.CodexMonitor.Core.Models;

namespace LuoIsHere.CodexMonitor.Core.Refresh;

public sealed class QuotaRefreshService : IDisposable
{
    private readonly IQuotaProvider _provider;
    private readonly IAppLogger _logger;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private QuotaMonitorState _state = new(null, false, null, null);
    private bool _disposed;

    public QuotaRefreshService(IQuotaProvider provider, IAppLogger logger)
    {
        _provider = provider;
        _logger = logger;
    }

    public event EventHandler<QuotaMonitorState>? StateChanged;

    public QuotaMonitorState State => _state;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!await _refreshGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            SetState(_state with
            {
                IsRefreshing = true,
                Error = null,
                LastAttemptAt = DateTimeOffset.Now,
            });

            var result = await _provider.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                SetState(_state with { IsRefreshing = false });
                return;
            }

            if (result.IsSuccess)
            {
                SetState(new QuotaMonitorState(result.Snapshot, false, null, DateTimeOffset.Now));
                _logger.Info("Codex quota refreshed successfully.");
            }
            else
            {
                var error = string.IsNullOrWhiteSpace(result.Error) ? "未知读取错误" : result.Error;
                SetState(_state with { IsRefreshing = false, Error = error });
                _logger.Error($"Codex quota refresh failed: {error}");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SetState(_state with { IsRefreshing = false });
        }
        catch (Exception exception)
        {
            SetState(_state with { IsRefreshing = false, Error = "刷新时发生未处理错误" });
            _logger.Error("Unexpected quota refresh failure.", exception);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    public async Task RunPeriodicAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            await RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private void SetState(QuotaMonitorState state)
    {
        _state = state;
        StateChanged?.Invoke(this, state);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _refreshGate.Dispose();
    }
}
