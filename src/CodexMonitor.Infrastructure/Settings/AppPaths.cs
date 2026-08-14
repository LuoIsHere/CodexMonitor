namespace LuoIsHere.CodexMonitor.Infrastructure.Settings;

public static class AppPaths
{
    public static string StateDirectory
    {
        get
        {
            var overridden = Environment.GetEnvironmentVariable("CODEX_MONITOR_HOME");
            if (!string.IsNullOrWhiteSpace(overridden))
            {
                return Path.GetFullPath(Environment.ExpandEnvironmentVariables(overridden));
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CodexMonitor");
        }
    }

    public static string SettingsFile => Path.Combine(StateDirectory, "settings.json");

    public static string LogDirectory => Path.Combine(StateDirectory, "logs");
}

