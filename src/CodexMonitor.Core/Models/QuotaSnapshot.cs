namespace LuoIsHere.CodexMonitor.Core.Models;

public sealed record QuotaSnapshot(
    string? LimitId,
    string? LimitName,
    CodexAccountInfo Account,
    QuotaWindow? FiveHour,
    QuotaWindow? Weekly,
    DateTimeOffset ObservedAt)
{
    public string? PlanType => Account.PlanType;
}
