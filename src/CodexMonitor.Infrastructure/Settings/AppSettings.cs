namespace LuoIsHere.CodexMonitor.Infrastructure.Settings;

public sealed record AppSettings
{
    public int SchemaVersion { get; init; } = 2;

    public int RefreshIntervalMinutes { get; init; } = 3;

    public string? CodexExecutable { get; init; }

    public NotificationSettings Notifications { get; init; } = new();

    public DisplaySettings Display { get; init; } = new();
}

public sealed record NotificationSettings
{
    public bool Enabled { get; init; } = true;
}

public sealed record DisplaySettings
{
    public bool ShowFiveHourQuota { get; init; } = true;

    public bool ShowWeeklyQuota { get; init; } = true;

    public bool ShowResetTimes { get; init; } = true;

    public bool ShowSubscription { get; init; } = true;
}
