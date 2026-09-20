using System.Runtime.Versioning;
using Microsoft.Win32;

namespace LuoIsHere.CodexMonitor.Infrastructure.Startup.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsRunRegistrationStore : IStartupRegistrationStore
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CodexMonitor";

    public string? Read()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: false);
        var value = key?.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return value switch
        {
            null => null,
            string command => command,
            _ => throw new InvalidOperationException("CodexMonitor 启动项不是字符串，请在 Windows 中检查。"),
        };
    }

    public void Write(string command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true)
            ?? throw new IOException("无法打开当前用户的启动项。");
        key.SetValue(ValueName, command, RegistryValueKind.String);
    }

    public void Delete()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
