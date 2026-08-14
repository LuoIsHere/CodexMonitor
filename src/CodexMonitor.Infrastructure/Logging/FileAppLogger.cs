using System.Globalization;
using LuoIsHere.CodexMonitor.Core.Abstractions;
using LuoIsHere.CodexMonitor.Infrastructure.Settings;

namespace LuoIsHere.CodexMonitor.Infrastructure.Logging;

public sealed class FileAppLogger : IAppLogger
{
    private const long MaximumLogBytes = 1_048_576;
    private readonly object _sync = new();
    private readonly string _logFile;

    public FileAppLogger()
    {
        Directory.CreateDirectory(AppPaths.LogDirectory);
        _logFile = Path.Combine(AppPaths.LogDirectory, "codex-monitor.log");
    }

    public void Info(string message) => Write("INFO", message, null);

    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        lock (_sync)
        {
            try
            {
                RotateIfNeeded();
                var line = $"{DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture)} [{level}] {message}";
                if (exception is not null)
                {
                    line += $" | {exception.GetType().Name}: {exception.Message}";
                }

                File.AppendAllText(_logFile, line + Environment.NewLine);
            }
            catch
            {
                // Logging must never terminate the monitor.
            }
        }
    }

    private void RotateIfNeeded()
    {
        if (!File.Exists(_logFile) || new FileInfo(_logFile).Length < MaximumLogBytes)
        {
            return;
        }

        File.Move(_logFile, _logFile + ".1", overwrite: true);
    }
}

