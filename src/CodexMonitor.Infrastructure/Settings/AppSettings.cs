namespace LuoIsHere.CodexMonitor.Infrastructure.Settings;

public sealed record AppSettings
{
    public string Language { get; init; } = "zh-CN";

    public int SchemaVersion { get; init; } = 4;

    public StartupSettings Startup { get; init; } = new();

    public int RefreshIntervalMinutes { get; init; } = 3;

    public string? CodexExecutable { get; init; }

    public NotificationSettings Notifications { get; init; } = new();

    public DisplaySettings Display { get; init; } = new();

    public FloatingWindowSettings FloatingWindow { get; init; } = new();
}

public sealed record StartupSettings
{
    public bool Enabled { get; init; }

    public bool MinimizeToTray { get; init; } = true;
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

public sealed record FloatingWindowSettings
{
    public bool Enabled { get; init; }

    public bool IsLocked { get; init; }

    public double? Left { get; init; }

    public double? Top { get; init; }

    public FloatingWindowDisplaySettings Display { get; init; } = new();
}

public sealed record FloatingWindowDisplaySettings
{
    public bool ShowFiveHourQuota { get; init; } = true;

    public bool ShowWeeklyQuota { get; init; } = true;

    public bool ShowLastRefreshTime { get; init; } = true;

    public bool ShowResetTimes { get; init; }

    public bool ShowSubscription { get; init; }
}
