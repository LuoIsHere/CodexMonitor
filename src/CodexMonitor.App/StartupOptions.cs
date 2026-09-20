using LuoIsHere.CodexMonitor.Infrastructure.Settings;

namespace LuoIsHere.CodexMonitor.App;

public sealed record StartupOptions(bool IsAutomatic)
{
    public static StartupOptions Parse(IEnumerable<string> arguments)
        => new(arguments.Any(argument =>
            string.Equals(argument, "--autostart", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(argument, "-autostart", StringComparison.OrdinalIgnoreCase)));

    public bool ShouldShowMainWindow(StartupSettings settings)
        => !IsAutomatic || !settings.MinimizeToTray;

    public async Task HandleSecondaryInstanceAsync(Func<Task<bool>> requestActivation)
    {
        if (!IsAutomatic)
        {
            _ = await requestActivation();
        }
    }
}
