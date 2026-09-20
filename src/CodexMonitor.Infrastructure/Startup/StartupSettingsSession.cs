using LuoIsHere.CodexMonitor.Core.Localization;
using LuoIsHere.CodexMonitor.Infrastructure.Settings;

namespace LuoIsHere.CodexMonitor.Infrastructure.Startup;

// Exists only while editing settings. Ordinary configuration saves never use this class.
public sealed class StartupSettingsSession
{
    private readonly UserStartupService _startup;
    private readonly Func<AppSettings, Task> _saveSettings;
    private bool _appliedEnabled;
    private bool _hasUnpersistedStartupChange;

    public StartupSettingsSession(
        AppSettings settings,
        UserStartupService startup,
        Func<AppSettings, Task> saveSettings)
    {
        _appliedEnabled = settings.Startup.Enabled;
        _startup = startup;
        _saveSettings = saveSettings;
    }

    public async Task<SettingsSaveResult> SaveAsync(AppSettings settings, bool registerCurrentPath)
    {
        var applied = false;
        try
        {
            if (settings.Startup.Enabled != _appliedEnabled ||
                (settings.Startup.Enabled && registerCurrentPath))
            {
                _startup.Apply(settings.Startup.Enabled);
                _appliedEnabled = settings.Startup.Enabled;
                _hasUnpersistedStartupChange = true;
                applied = true;
            }
        }
        catch (Exception exception) when (UserStartupService.IsExpectedFailure(exception))
        {
            return new(false, false, AppText.Get("StartupSaveFailed", exception.Message));
        }

        try
        {
            await _saveSettings(settings);
            _hasUnpersistedStartupChange = false;
            return new(true, applied, null);
        }
        catch (Exception exception) when (UserStartupService.IsExpectedFailure(exception))
        {
            var prefix = _hasUnpersistedStartupChange ? AppText.Get("StartupPartialFailure") : AppText.Get("SettingsSaveFailed");
            return new(false, applied, AppText.Get("SettingsRetryHint", prefix, exception.Message));
        }
    }
}

public sealed record SettingsSaveResult(bool Success, bool StartupOperationCompleted, string? Error);
