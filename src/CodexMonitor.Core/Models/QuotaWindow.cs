namespace LuoIsHere.CodexMonitor.Core.Models;

public sealed record QuotaWindow(
    string Label,
    double? UsedPercent,
    double? RemainingPercent,
    int? WindowDurationMinutes,
    DateTimeOffset? ResetsAt);

