using System.Runtime.Versioning;
using LuoIsHere.CodexMonitor.Core.Localization;
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
            _ => throw new InvalidOperationException(AppText.Get("RunValueInvalid")),
        };
    }

    public void Write(string command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true)
            ?? throw new IOException(AppText.Get("RunKeyUnavailable"));
        key.SetValue(ValueName, command, RegistryValueKind.String);
    }

    public void Delete()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
