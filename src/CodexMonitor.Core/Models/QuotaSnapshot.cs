namespace LuoIsHere.CodexMonitor.Core.Models;

public sealed record QuotaSnapshot(
    string? LimitId,
    string? LimitName,
    string? PlanType,
    QuotaWindow? FiveHour,
    QuotaWindow? Weekly,
    DateTimeOffset ObservedAt);

