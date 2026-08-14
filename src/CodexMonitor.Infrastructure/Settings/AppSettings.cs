namespace LuoIsHere.CodexMonitor.Infrastructure.Settings;

public sealed record AppSettings
{
    public int SchemaVersion { get; init; } = 1;

    public int RefreshIntervalMinutes { get; init; } = 3;

    public string? CodexExecutable { get; init; }
}

