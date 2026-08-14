using LuoIsHere.CodexMonitor.Infrastructure.Settings;

namespace LuoIsHere.CodexMonitor.App;

public interface IFloatingWindowService : IDisposable
{
    bool IsAvailable { get; }

    bool IsEnabled { get; }

    bool IsLocked { get; }

    FloatingWindowSettings CurrentSettings { get; }

    event EventHandler? StateChanged;

    event EventHandler<FloatingWindowSettingsChangedEventArgs>? SettingsChanged;

    void SetEnabled(bool enabled);

    void SetLocked(bool isLocked);

    void ApplySettings(FloatingWindowSettings settings);
}

public sealed class FloatingWindowSettingsChangedEventArgs(FloatingWindowSettings settings) : EventArgs
{
    public FloatingWindowSettings Settings { get; } = settings;
}
