using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;

namespace LuoIsHere.CodexMonitor.App;

internal sealed class SingleInstanceCoordinator : IAsyncDisposable
{
    private const string ActivationMessage = "activate";
    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _lifetime = new();
    private Task? _listenerTask;
    private bool _disposed;

    public SingleInstanceCoordinator()
    {
        var identity = GetInstanceIdentity();
        _pipeName = $"LuoIsHere.CodexMonitor.{identity}";
        _mutex = new Mutex(
            initiallyOwned: true,
            $"Local\\LuoIsHere.CodexMonitor.{identity}",
            out var createdNew);
        IsPrimaryInstance = createdNew;
    }

    public event EventHandler? ActivationRequested;

    public bool IsPrimaryInstance { get; }

    public void StartListening()
    {
        if (!IsPrimaryInstance || _listenerTask is not null)
        {
            return;
        }

        _listenerTask = ListenAsync(_lifetime.Token);
    }

    public async Task<bool> RequestActivationAsync(CancellationToken cancellationToken = default)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(3);
        while (DateTimeOffset.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var client = new NamedPipeClientStream(
                    ".",
                    _pipeName,
                    PipeDirection.Out,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await client.ConnectAsync(400, cancellationToken).ConfigureAwait(false);
                await using var writer = new StreamWriter(client, leaveOpen: true)
                {
                    AutoFlush = true,
                };
                await writer.WriteLineAsync(ActivationMessage.AsMemory(), cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }
            catch (Exception exception) when (
                exception is TimeoutException or IOException &&
                !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }

        return false;
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await using var server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                using var reader = new StreamReader(server, leaveOpen: true);
                var message = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (string.Equals(message, ActivationMessage, StringComparison.Ordinal))
                {
                    ActivationRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during application shutdown.
        }
    }

    private static string GetInstanceIdentity()
    {
        var userIdentity = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        var sessionId = Process.GetCurrentProcess().SessionId;
        var rawIdentity = $"{userIdentity}.{sessionId}";
        return string.Concat(rawIdentity.Select(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_'
                ? character
                : '_'));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _lifetime.CancelAsync();
        if (_listenerTask is not null)
        {
            await _listenerTask;
        }

        if (IsPrimaryInstance)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
        _lifetime.Dispose();
    }
}
