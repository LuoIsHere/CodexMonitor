using System.Reflection;
using LuoIsHere.CodexMonitor.Core.Localization;

namespace LuoIsHere.CodexMonitor.Infrastructure.Startup;

public sealed class StartupExecutableResolver
{
    private readonly Func<string?> _processPath;
    private readonly Func<string?> _entryAssemblyName;
    private readonly Func<string, bool> _fileExists;

    public StartupExecutableResolver(
        Func<string?>? processPath = null,
        Func<string?>? entryAssemblyName = null,
        Func<string, bool>? fileExists = null)
    {
        _processPath = processPath ?? (() => Environment.ProcessPath);
        _entryAssemblyName = entryAssemblyName ?? (() => Assembly.GetEntryAssembly()?.GetName().Name);
        _fileExists = fileExists ?? File.Exists;
    }

    public string Resolve()
    {
        var path = _processPath();
        if (_entryAssemblyName() != "CodexMonitor" || string.IsNullOrWhiteSpace(path) ||
            !Path.IsPathFullyQualified(path) ||
            !string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(path), "dotnet.exe", StringComparison.OrdinalIgnoreCase) ||
            path.IndexOfAny(['"', '\r', '\n']) >= 0)
        {
            throw new InvalidOperationException(AppText.Get("StartupTargetUnknown"));
        }

        path = Path.GetFullPath(path);
        if (!_fileExists(path))
        {
            throw new InvalidOperationException(AppText.Get("StartupTargetMissing"));
        }

        // Environment.ProcessPath identifies the apphost, including single-file publishing.
        // Assembly.Location and extracted runtime paths must never be used as fallbacks.
        return path;
    }
}
