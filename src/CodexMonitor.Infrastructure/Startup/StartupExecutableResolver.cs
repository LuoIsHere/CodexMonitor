using System.Reflection;

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
            throw new InvalidOperationException("无法确定应用的实际 EXE。请直接运行发布的 CodexMonitor EXE 后再设置自启。");
        }

        path = Path.GetFullPath(path);
        if (!_fileExists(path))
        {
            throw new InvalidOperationException("应用 EXE 已不存在，请从有效位置运行后重新登记。");
        }

        // Environment.ProcessPath identifies the apphost, including single-file publishing.
        // Assembly.Location and extracted runtime paths must never be used as fallbacks.
        return path;
    }
}
